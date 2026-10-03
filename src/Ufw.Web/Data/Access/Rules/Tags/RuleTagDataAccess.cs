using Microsoft.EntityFrameworkCore;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Data.Extensions;
using Ufw.Web.Data.Model;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;

namespace Ufw.Web.Data.Access.Rules.Tags;

internal sealed class RuleTagDataAccess(ITransactionServiceHandle transactionService)
    : DatabaseService<ApplicationDbContext>(transactionService), IRuleTagDataAccess
{
    public Task<IReadOnlyList<RuleTagItem>> GetAsync(CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunReadOnlyAsync(context => GetCoreAsync(context, cancellationToken));

    public Task<DataMutationResult> CreateAsync(string name, string color, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync(async (context, transaction) =>
        {
            if (await NameExistsAsync(context, name, excludingId: null, cancellationToken))
            {
                return transaction.Rollback(DataMutationResult.Failure(new DataMutationUniqueConflictError()));
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
            catch (DbUpdateException exception) when (exception.TryGetDataMutationError(out DataMutationError? error) && error is DataMutationUniqueConflictError)
            {
                return transaction.Rollback(DataMutationResult.Failure(error));
            }

            return transaction.Commit(DataMutationResult.Success());
        });

    public Task<DataMutationResult> UpdateAsync(Guid publicId, string name, string color, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync(async (context, transaction) =>
        {
            RuleTagEntry? tag = await context.Set<RuleTagEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == publicId, cancellationToken);
            if (tag is null)
            {
                return transaction.Rollback(DataMutationResult.Failure(new DataMutationNotFoundError()));
            }
            if (await NameExistsAsync(context, name, tag.Id, cancellationToken))
            {
                return transaction.Rollback(DataMutationResult.Failure(new DataMutationUniqueConflictError()));
            }

            tag.Name = name;
            tag.Color = color;
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.TryGetDataMutationError(out DataMutationError? error) && error is DataMutationUniqueConflictError)
            {
                return transaction.Rollback(DataMutationResult.Failure(error));
            }

            return transaction.Commit(DataMutationResult.Success());
        });

    public Task<DataMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync(async (context, transaction) =>
        {
            RuleTagEntry? tag = await context.Set<RuleTagEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == publicId, cancellationToken);
            if (tag is null)
            {
                return transaction.Rollback(DataMutationResult.Failure(new DataMutationNotFoundError()));
            }

            bool inUse = await context.Set<RuleMetadataTagEntry>().AnyAsync(relation => relation.TagId == tag.Id, cancellationToken)
                || await context.Set<RuleTemplateTagEntry>().AnyAsync(relation => relation.TagId == tag.Id, cancellationToken);
            if (inUse)
            {
                return transaction.Rollback(DataMutationResult.Failure(new DataMutationReferenceConflictError()));
            }

            context.Remove(tag);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.TryGetDataMutationError(out DataMutationError? error) && error is DataMutationReferenceConflictError)
            {
                return transaction.Rollback(DataMutationResult.Failure(error));
            }

            return transaction.Commit(DataMutationResult.Success());
        });

    private static async Task<IReadOnlyList<RuleTagItem>> GetCoreAsync(ApplicationDbContext context, CancellationToken cancellationToken) =>
        await context.Set<RuleTagEntry>()
            .AsNoTracking()
            .OrderBy(static tag => tag.Name)
            .ThenBy(static tag => tag.PublicId)
            .Select(static tag => new RuleTagItem(tag.PublicId, tag.Name, tag.Color))
            .ToListAsync(cancellationToken);

    private static async Task<bool> NameExistsAsync(ApplicationDbContext context, string name, long? excludingId, CancellationToken cancellationToken)
    {
        IQueryable<RuleTagEntry> query = context.Set<RuleTagEntry>().AsNoTracking();
        if (excludingId.HasValue)
        {
            query = query.Where(tag => tag.Id != excludingId.Value);
        }

        return await query.AnyAsync(tag => tag.Name == name, cancellationToken);
    }
}
