using Microsoft.EntityFrameworkCore;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Data.Extensions;
using Ufw.Web.Data.Model;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;

namespace Ufw.Web.Data.Access.Rules.Groups;

internal sealed class RuleGroupDataAccess(ITransactionServiceHandle transactionService)
    : DatabaseService<ApplicationDbContext>(transactionService), IRuleGroupDataAccess
{
    public Task<IReadOnlyList<RuleGroupItem>> GetAsync(CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunReadOnlyAsync(context => GetCoreAsync(context, cancellationToken));

    public Task<DataMutationResult> CreateAsync(string name, string? comment, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync(async (context, transaction) =>
        {
            bool duplicateName = await NameExistsAsync(context, name, excludingId: null, cancellationToken);
            if (duplicateName)
            {
                return transaction.Rollback(DataMutationResult.Failure(new DataMutationUniqueConflictError()));
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
            catch (DbUpdateException exception) when (exception.TryGetDataMutationError(out DataMutationError? error) && error is DataMutationUniqueConflictError)
            {
                return transaction.Rollback(DataMutationResult.Failure(error));
            }

            return transaction.Commit(DataMutationResult.Success());
        });

    public Task<DataMutationResult> UpdateAsync(Guid publicId, string name, string? comment, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync(async (context, transaction) =>
        {
            RuleGroupEntry? group = await context.Set<RuleGroupEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == publicId, cancellationToken);
            if (group is null)
            {
                return transaction.Rollback(DataMutationResult.Failure(new DataMutationNotFoundError()));
            }
            bool duplicateName = await NameExistsAsync(context, name, group.Id, cancellationToken);
            if (duplicateName)
            {
                return transaction.Rollback(DataMutationResult.Failure(new DataMutationUniqueConflictError()));
            }

            group.Name = name;
            group.Comment = comment;
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
            RuleGroupEntry? group = await context.Set<RuleGroupEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == publicId, cancellationToken);
            if (group is null)
            {
                return transaction.Rollback(DataMutationResult.Failure(new DataMutationNotFoundError()));
            }

            bool inUse = await context.Set<RuleMetadataEntry>().AnyAsync(metadata => metadata.GroupId == group.Id, cancellationToken)
                || await context.Set<RuleTemplateEntry>().AnyAsync(template => template.GroupId == group.Id, cancellationToken);
            if (inUse)
            {
                return transaction.Rollback(DataMutationResult.Failure(new DataMutationReferenceConflictError()));
            }

            context.Remove(group);
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

    private static async Task<IReadOnlyList<RuleGroupItem>> GetCoreAsync(ApplicationDbContext context, CancellationToken cancellationToken) =>
        await context.Set<RuleGroupEntry>()
            .AsNoTracking()
            .AsSplitQuery()
            .OrderBy(static group => group.Name)
            .ThenBy(static group => group.PublicId)
            .Select(static group => new RuleGroupItem
            {
                Id = group.PublicId,
                Name = group.Name,
                Comment = group.Comment,
                RuleIds = group.RuleMetadata
                    .OrderBy(static metadata => metadata.RuleId)
                    .Select(static metadata => metadata.RuleId)
                    .ToList(),
                TemplateIds = group.RuleTemplates
                    .OrderBy(static template => template.PublicId)
                    .Select(static template => template.PublicId)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

    private static async Task<bool> NameExistsAsync(ApplicationDbContext context, string name, long? excludingId, CancellationToken cancellationToken)
    {
        IQueryable<RuleGroupEntry> query = context.Set<RuleGroupEntry>().AsNoTracking();
        if (excludingId.HasValue)
        {
            query = query.Where(group => group.Id != excludingId.Value);
        }

        return await query.AnyAsync(group => group.Name == name, cancellationToken);
    }
}
