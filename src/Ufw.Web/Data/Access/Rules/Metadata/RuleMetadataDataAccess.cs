using Microsoft.EntityFrameworkCore;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Data.Extensions;
using Ufw.Web.Data.Model;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;

namespace Ufw.Web.Data.Access.Rules.Metadata;

internal sealed class RuleMetadataDataAccess(ITransactionServiceHandle transactionService)
    : DatabaseService<ApplicationDbContext>(transactionService), IRuleMetadataDataAccess
{
    public Task<IReadOnlyList<RuleMetadataItem>> GetForRuleIdsAsync(IReadOnlyCollection<string> ruleIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ruleIds);
        string[] identities = [.. ruleIds.Distinct(StringComparer.Ordinal)];
        if (identities.Length == 0)
        {
            return Task.FromResult<IReadOnlyList<RuleMetadataItem>>([]);
        }

        return Transaction.Scoped.RunReadOnlyAsync(async context =>
        {
            List<RuleMetadataItem> metadata = await CreateMetadataQuery(context)
                .Where(entry => identities.Contains(entry.RuleId))
                .OrderBy(static entry => entry.RuleId)
                .Select(static entry => new RuleMetadataItem
                {
                    Id = entry.PublicId,
                    RuleId = entry.RuleId,
                    Notes = entry.Notes,
                    Tags = entry.Tags
                        .Select(static relation => relation.Tag)
                        .OrderBy(static tag => tag.Name)
                        .ThenBy(static tag => tag.PublicId)
                        .Select(static tag => new RuleTagItem(tag.PublicId, tag.Name, tag.Color))
                        .ToList(),
                    Group = entry.Group == null ? null : new RuleGroupSummary(entry.Group.PublicId, entry.Group.Name, entry.Group.Comment),
                })
                .ToListAsync(cancellationToken);
            IReadOnlyList<RuleMetadataItem> result = metadata;
            return result;
        });
    }

    public Task<IReadOnlyList<RuleMetadataItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunReadOnlyAsync(async context =>
        {
            List<RuleMetadataItem> metadata = await CreateMetadataQuery(context)
                .OrderBy(static entry => entry.RuleId)
                .Select(static entry => new RuleMetadataItem
                {
                    Id = entry.PublicId,
                    RuleId = entry.RuleId,
                    Notes = entry.Notes,
                    Tags = entry.Tags
                        .Select(static relation => relation.Tag)
                        .OrderBy(static tag => tag.Name)
                        .ThenBy(static tag => tag.PublicId)
                        .Select(static tag => new RuleTagItem(tag.PublicId, tag.Name, tag.Color))
                        .ToList(),
                    Group = entry.Group == null ? null : new RuleGroupSummary(entry.Group.PublicId, entry.Group.Name, entry.Group.Comment),
                })
                .ToListAsync(cancellationToken);
            IReadOnlyList<RuleMetadataItem> result = metadata;
            return result;
        });

    public Task<DataMutationResult<RuleMetadataItem?>> SaveAsync(string ruleId, RuleMetadataValues values, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentNullException.ThrowIfNull(values);

        return Transaction.Scoped.RunAsync(async (context, transaction) =>
        {
            RuleMetadataEntry? metadata = await context.Set<RuleMetadataEntry>()
                .Include(static entry => entry.Tags)
                .SingleOrDefaultAsync(entry => entry.RuleId == ruleId, cancellationToken);

            if (values.IsEmpty)
            {
                if (metadata is not null)
                {
                    context.Remove(metadata);
                    await context.SaveChangesAsync(cancellationToken);
                }
                return transaction.Commit(DataMutationResult.Success<RuleMetadataItem?>(value: null));
            }

            RuleManagementDependencies dependencies = await RuleManagementDependencyResolver.ResolveAsync(context, values.TagIds, values.GroupId, cancellationToken);
            if (dependencies.Error is { } dependencyError)
            {
                return transaction.Rollback(DataMutationResult.Failure<RuleMetadataItem?>(dependencyError));
            }

            if (metadata is null)
            {
                metadata = new RuleMetadataEntry { RuleId = ruleId };
                context.Add(metadata);
            }

            metadata.Notes = values.Notes;
            metadata.Group = dependencies.Group;
            metadata.GroupId = dependencies.Group?.Id;
            RuleTagRelationSynchronizer.Synchronize(
                context,
                metadata.Tags,
                dependencies.Tags,
                static relation => relation.TagId,
                tag => new RuleMetadataTagEntry { RuleMetadata = metadata, Tag = tag });

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.TryGetDataMutationError(out DataMutationError? error) && error is DataMutationReferenceConflictError)
            {
                return transaction.Rollback(DataMutationResult.Failure<RuleMetadataItem?>(error));
            }
            RuleTagItem[] tags = [.. dependencies.Tags
                .OrderBy(static tag => tag.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static tag => tag.PublicId)
                .Select(static tag => new RuleTagItem(tag.PublicId, tag.Name, tag.Color))];
            RuleGroupSummary? group = dependencies.Group is null
                ? null
                : new RuleGroupSummary(dependencies.Group.PublicId, dependencies.Group.Name, dependencies.Group.Comment);
            RuleMetadataItem item = new(metadata.PublicId, metadata.RuleId, metadata.Notes, tags, group);
            return transaction.Commit(DataMutationResult.Success<RuleMetadataItem?>(item));
        });
    }

    public Task<RuleMetadataReplacementPersistenceOutcome> ReconcileReplacementAsync(string originalRuleId, string replacementRuleId, bool originalRuleStillLive, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalRuleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(replacementRuleId);
        if (string.Equals(originalRuleId, replacementRuleId, StringComparison.Ordinal))
        {
            return Task.FromResult(RuleMetadataReplacementPersistenceOutcome.Unchanged);
        }

        return Transaction.Scoped.RunAsync<RuleMetadataReplacementPersistenceOutcome>(async (context, transaction) =>
        {
            string[] ruleIds = [originalRuleId, replacementRuleId];
            List<RuleMetadataEntry> metadata = await context.Set<RuleMetadataEntry>()
                .Include(static entry => entry.Tags)
                .ThenInclude(static relation => relation.Tag)
                .Where(entry => ruleIds.Contains(entry.RuleId))
                .ToListAsync(cancellationToken);
            RuleMetadataEntry? source = metadata.SingleOrDefault(entry => string.Equals(entry.RuleId, originalRuleId, StringComparison.Ordinal));
            RuleMetadataEntry? staleTarget = metadata.SingleOrDefault(entry => string.Equals(entry.RuleId, replacementRuleId, StringComparison.Ordinal));

            if (staleTarget is not null)
            {
                context.Remove(staleTarget);
            }

            if (source is null)
            {
                if (staleTarget is not null)
                {
                    await context.SaveChangesAsync(cancellationToken);
                }
                return transaction.Commit(staleTarget is null ? RuleMetadataReplacementPersistenceOutcome.Unchanged : RuleMetadataReplacementPersistenceOutcome.ClearedStaleTarget);
            }

            RuleMetadataReplacementPersistenceOutcome outcome;
            if (originalRuleStillLive)
            {
                RuleMetadataEntry copy = new()
                {
                    RuleId = replacementRuleId,
                    Notes = source.Notes,
                    GroupId = source.GroupId,
                };
                foreach (RuleMetadataTagEntry relation in source.Tags)
                {
                    copy.Tags.Add(new RuleMetadataTagEntry
                    {
                        RuleMetadata = copy,
                        Tag = relation.Tag,
                    });
                }
                context.Add(copy);
                outcome = RuleMetadataReplacementPersistenceOutcome.Copied;
            }
            else
            {
                source.RuleId = replacementRuleId;
                outcome = RuleMetadataReplacementPersistenceOutcome.Rekeyed;
            }

            await context.SaveChangesAsync(cancellationToken);
            return transaction.Commit(outcome);
        });
    }

    public Task<bool> DeleteAsync(string ruleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        return Transaction.Scoped.RunAsync<bool>(async (context, transaction) =>
        {
            RuleMetadataEntry? metadata = await context.Set<RuleMetadataEntry>()
                .SingleOrDefaultAsync(entry => entry.RuleId == ruleId, cancellationToken);
            if (metadata is null)
            {
                return transaction.Commit(false);
            }

            context.Remove(metadata);
            await context.SaveChangesAsync(cancellationToken);
            return transaction.Commit(true);
        });
    }

    public Task<int> DeleteForRuleIdsAsync(IReadOnlyCollection<string> ruleIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ruleIds);
        string[] identities = [.. ruleIds.Distinct(StringComparer.Ordinal)];
        if (identities.Length == 0)
        {
            return Task.FromResult(0);
        }

        return Transaction.Scoped.RunAsync<int>(async (context, transaction) =>
        {
            int removedCount = await context.Set<RuleMetadataEntry>()
                .Where(entry => identities.Contains(entry.RuleId))
                .ExecuteDeleteAsync(cancellationToken);
            return transaction.Commit(removedCount);
        });
    }

    public Task<int> DeleteUnmatchedAsync(IReadOnlyCollection<Guid> metadataIds, IReadOnlyCollection<string> liveRuleIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadataIds);
        ArgumentNullException.ThrowIfNull(liveRuleIds);
        Guid[] identities = [.. metadataIds.Distinct()];
        if (identities.Length == 0)
        {
            return Task.FromResult(0);
        }

        string[] liveIdentities = [.. liveRuleIds.Distinct(StringComparer.Ordinal)];
        return Transaction.Scoped.RunAsync<int>(async (context, transaction) =>
        {
            IQueryable<RuleMetadataEntry> query = context.Set<RuleMetadataEntry>()
                .Where(entry => identities.Contains(entry.PublicId));
            if (liveIdentities.Length > 0)
            {
                query = query.Where(entry => !liveIdentities.Contains(entry.RuleId));
            }

            int removedCount = await query.ExecuteDeleteAsync(cancellationToken);
            return transaction.Commit(removedCount);
        });
    }

    private static IQueryable<RuleMetadataEntry> CreateMetadataQuery(ApplicationDbContext context) => context.Set<RuleMetadataEntry>().AsNoTracking();
}
