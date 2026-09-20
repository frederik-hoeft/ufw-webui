using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ufw.Web.Api.V1.Models.Rules;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;

namespace Ufw.Web.Services.Rules;

internal sealed class RuleTagRepository(ITransactionServiceHandle transactionService)
    : DatabaseService<ApplicationDbContext>(transactionService), IRuleTagRepository
{
    public Task<RuleTagInventoryResponse> GetAsync(CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunReadOnlyAsync(context => GetCoreAsync(context, cancellationToken));

    public Task<RuleTagMutationResult> CreateAsync(string name, string color, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<RuleTagMutationResult>(async (context, transaction) =>
        {
            if (await NameExistsAsync(context, name, excludingId: null, cancellationToken))
            {
                return transaction.Rollback(new RuleTagMutationResult(RuleTagMutationOutcome.NameConflict));
            }

            context.Add(new RuleTagEntry
            {
                Name = name,
                Color = color,
            });

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return transaction.Rollback(new RuleTagMutationResult(RuleTagMutationOutcome.NameConflict));
            }

            RuleTagInventoryResponse response = await GetCoreAsync(context, cancellationToken);
            return transaction.Commit(new RuleTagMutationResult(RuleTagMutationOutcome.Success, response));
        });

    public Task<RuleTagMutationResult> UpdateAsync(
        Guid publicId,
        string name,
        string color,
        CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<RuleTagMutationResult>(async (context, transaction) =>
        {
            RuleTagEntry? tag = await context.Set<RuleTagEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == publicId, cancellationToken);
            if (tag is null)
            {
                return transaction.Rollback(new RuleTagMutationResult(RuleTagMutationOutcome.NotFound));
            }
            if (await NameExistsAsync(context, name, tag.Id, cancellationToken))
            {
                return transaction.Rollback(new RuleTagMutationResult(RuleTagMutationOutcome.NameConflict));
            }

            tag.Name = name;
            tag.Color = color;
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return transaction.Rollback(new RuleTagMutationResult(RuleTagMutationOutcome.NameConflict));
            }

            RuleTagInventoryResponse response = await GetCoreAsync(context, cancellationToken);
            return transaction.Commit(new RuleTagMutationResult(RuleTagMutationOutcome.Success, response));
        });

    public Task<RuleTagMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<RuleTagMutationResult>(async (context, transaction) =>
        {
            RuleTagEntry? tag = await context.Set<RuleTagEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == publicId, cancellationToken);
            if (tag is null)
            {
                return transaction.Rollback(new RuleTagMutationResult(RuleTagMutationOutcome.NotFound));
            }

            bool inUse = await context.Set<RuleMetadataTagEntry>().AnyAsync(relation => relation.TagId == tag.Id, cancellationToken);
            if (inUse)
            {
                return transaction.Rollback(new RuleTagMutationResult(RuleTagMutationOutcome.InUse));
            }

            context.Remove(tag);
            await context.SaveChangesAsync(cancellationToken);
            RuleTagInventoryResponse response = await GetCoreAsync(context, cancellationToken);
            return transaction.Commit(new RuleTagMutationResult(RuleTagMutationOutcome.Success, response));
        });

    private static async Task<RuleTagInventoryResponse> GetCoreAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        RuleTagItem[] tags = await context.Set<RuleTagEntry>()
            .AsNoTracking()
            .OrderBy(static tag => tag.Name)
            .ThenBy(static tag => tag.PublicId)
            .Select(static tag => new RuleTagItem(tag.PublicId, tag.Name, tag.Color))
            .ToArrayAsync(cancellationToken);
        return new RuleTagInventoryResponse(tags);
    }

    private static async Task<bool> NameExistsAsync(ApplicationDbContext context, string name, long? excludingId, CancellationToken cancellationToken)
    {
        IQueryable<RuleTagEntry> query = context.Set<RuleTagEntry>().AsNoTracking();
        if (excludingId.HasValue)
        {
            query = query.Where(tag => tag.Id != excludingId.Value);
        }

        return await query.AnyAsync(tag => tag.Name == name, cancellationToken);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
