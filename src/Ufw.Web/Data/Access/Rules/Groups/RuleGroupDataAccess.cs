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

    public Task<RuleGroupMutationResult> CreateAsync(string name, string? comment, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync(async (context, transaction) =>
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
            catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation)
            {
                return transaction.Rollback(new RuleGroupMutationResult(RuleGroupMutationOutcome.NameConflict));
            }

            return transaction.Commit(new RuleGroupMutationResult(RuleGroupMutationOutcome.Success));
        });

    public Task<RuleGroupMutationResult> UpdateAsync(Guid publicId, string name, string? comment, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync(async (context, transaction) =>
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
            catch (DbUpdateException exception) when (exception.IsUniqueConstraintViolation)
            {
                return transaction.Rollback(new RuleGroupMutationResult(RuleGroupMutationOutcome.NameConflict));
            }

            return transaction.Commit(new RuleGroupMutationResult(RuleGroupMutationOutcome.Success));
        });

    public Task<RuleGroupMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync(async (context, transaction) =>
        {
            RuleGroupEntry? group = await context.Set<RuleGroupEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == publicId, cancellationToken);
            if (group is null)
            {
                return transaction.Rollback(new RuleGroupMutationResult(RuleGroupMutationOutcome.NotFound));
            }

            bool inUse = await context.Set<RuleMetadataEntry>().AnyAsync(metadata => metadata.GroupId == group.Id, cancellationToken)
                || await context.Set<RuleTemplateEntry>().AnyAsync(template => template.GroupId == group.Id, cancellationToken);
            if (inUse)
            {
                return transaction.Rollback(new RuleGroupMutationResult(RuleGroupMutationOutcome.InUse));
            }

            context.Remove(group);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.IsForeignKeyConstraintViolation)
            {
                return transaction.Rollback(new RuleGroupMutationResult(RuleGroupMutationOutcome.InUse));
            }

            return transaction.Commit(new RuleGroupMutationResult(RuleGroupMutationOutcome.Success));
        });

    private static async Task<IReadOnlyList<RuleGroupItem>> GetCoreAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        RuleGroupEntry[] groups = await context.Set<RuleGroupEntry>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(static group => group.RuleMetadata)
            .Include(static group => group.RuleTemplates)
            .OrderBy(static group => group.Name)
            .ThenBy(static group => group.PublicId)
            .ToArrayAsync(cancellationToken);
        return
        [
            .. groups.Select(static group => new RuleGroupItem
            {
                Id = group.PublicId,
                Name = group.Name,
                Comment = group.Comment,
                RuleIds = [.. group.RuleMetadata.Select(static metadata => metadata.RuleId).Order(StringComparer.Ordinal)],
                TemplateIds = [.. group.RuleTemplates.Select(static template => template.PublicId).Order()],
            })
        ];
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
}
