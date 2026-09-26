using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Ufw.Web.Model.V1.RuleGroups;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;

namespace Ufw.Web.Services.Rules;

internal sealed class RuleGroupRepository(ITransactionServiceHandle transactionService)
    : DatabaseService<ApplicationDbContext>(transactionService), IRuleGroupRepository
{
    public Task<RuleGroupInventoryResponse> GetAsync(CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunReadOnlyAsync(context => GetCoreAsync(context, cancellationToken));

    public Task<RuleGroupMutationResult> CreateAsync(string name, string? comment, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<RuleGroupMutationResult>(async (context, transaction) =>
        {
            if (await NameExistsAsync(context, name, excludingId: null, cancellationToken))
            {
                return transaction.Rollback(new RuleGroupMutationResult(RuleGroupMutationOutcome.NameConflict));
            }

            context.Add(new RuleGroupEntry
            {
                Name = name,
                Comment = comment,
            });

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return transaction.Rollback(new RuleGroupMutationResult(RuleGroupMutationOutcome.NameConflict));
            }

            RuleGroupInventoryResponse response = await GetCoreAsync(context, cancellationToken);
            return transaction.Commit(new RuleGroupMutationResult(RuleGroupMutationOutcome.Success, response));
        });

    public Task<RuleGroupMutationResult> UpdateAsync(Guid publicId, string name, string? comment, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<RuleGroupMutationResult>(async (context, transaction) =>
        {
            RuleGroupEntry? group = await context.Set<RuleGroupEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == publicId, cancellationToken);
            if (group is null)
            {
                return transaction.Rollback(new RuleGroupMutationResult(RuleGroupMutationOutcome.NotFound));
            }
            if (await NameExistsAsync(context, name, group.Id, cancellationToken))
            {
                return transaction.Rollback(new RuleGroupMutationResult(RuleGroupMutationOutcome.NameConflict));
            }

            group.Name = name;
            group.Comment = comment;
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return transaction.Rollback(new RuleGroupMutationResult(RuleGroupMutationOutcome.NameConflict));
            }

            RuleGroupInventoryResponse response = await GetCoreAsync(context, cancellationToken);
            return transaction.Commit(new RuleGroupMutationResult(RuleGroupMutationOutcome.Success, response));
        });

    public Task<RuleGroupMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<RuleGroupMutationResult>(async (context, transaction) =>
        {
            RuleGroupEntry? group = await context.Set<RuleGroupEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == publicId, cancellationToken);
            if (group is null)
            {
                return transaction.Rollback(new RuleGroupMutationResult(RuleGroupMutationOutcome.NotFound));
            }

            bool inUse = await context.Set<RuleMetadataEntry>().AnyAsync(metadata => metadata.GroupId == group.Id, cancellationToken);
            if (inUse)
            {
                return transaction.Rollback(new RuleGroupMutationResult(RuleGroupMutationOutcome.InUse));
            }

            context.Remove(group);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsForeignKeyConstraintViolation(exception))
            {
                return transaction.Rollback(new RuleGroupMutationResult(RuleGroupMutationOutcome.InUse));
            }

            RuleGroupInventoryResponse response = await GetCoreAsync(context, cancellationToken);
            return transaction.Commit(new RuleGroupMutationResult(RuleGroupMutationOutcome.Success, response));
        });

    private static async Task<RuleGroupInventoryResponse> GetCoreAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        RuleGroupEntry[] groups = await context.Set<RuleGroupEntry>()
            .AsNoTracking()
            .Include(static group => group.RuleMetadata)
            .OrderBy(static group => group.Name)
            .ThenBy(static group => group.PublicId)
            .ToArrayAsync(cancellationToken);
        RuleGroupItem[] items = [.. groups.Select(static group => new RuleGroupItem
        {
            Id = group.PublicId,
            Name = group.Name,
            Comment = group.Comment,
            RuleIds = [.. group.RuleMetadata.Select(static metadata => metadata.RuleId).Order(StringComparer.Ordinal)],
        })];
        return new RuleGroupInventoryResponse { Groups = items };
    }

    private static async Task<bool> NameExistsAsync(ApplicationDbContext context, string name, long? excludingId, CancellationToken cancellationToken)
    {
        IQueryable<RuleGroupEntry> query = context.Set<RuleGroupEntry>().AsNoTracking();
        if (excludingId.HasValue)
        {
            query = query.Where(group => group.Id != excludingId.Value);
        }

        return await query.AnyAsync(group => group.Name == name, cancellationToken);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static bool IsForeignKeyConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation };
}
