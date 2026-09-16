using Microsoft.EntityFrameworkCore;
using Ufw.Web.Api.V1.Models.Rules;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;

namespace Ufw.Web.Services.Rules;

internal sealed class RuleMetadataRepository(ITransactionServiceHandle transactionService)
    : DatabaseService<ApplicationDbContext>(transactionService), IRuleMetadataRepository
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
            RuleMetadataEntry[] metadata = await context.Set<RuleMetadataEntry>()
                .AsNoTracking()
                .Include(static entry => entry.Tags)
                .ThenInclude(static relation => relation.Tag)
                .Where(entry => identities.Contains(entry.RuleId))
                .OrderBy(static entry => entry.RuleId)
                .ToArrayAsync(cancellationToken);
            IReadOnlyList<RuleMetadataItem> result = metadata.Select(ToItem).ToArray();
            return result;
        });
    }

    public Task<RuleMetadataSaveResult> SaveAsync(string ruleId, RuleMetadataValues values, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentNullException.ThrowIfNull(values);

        return Transaction.Scoped.RunAsync<RuleMetadataSaveResult>(async (context, transaction) =>
        {
            RuleMetadataEntry? metadata = await context.Set<RuleMetadataEntry>()
                .Include(static entry => entry.Tags)
                .ThenInclude(static relation => relation.Tag)
                .SingleOrDefaultAsync(entry => entry.RuleId == ruleId, cancellationToken);

            if (values.IsEmpty)
            {
                if (metadata is not null)
                {
                    context.Remove(metadata);
                    await context.SaveChangesAsync(cancellationToken);
                }
                return transaction.Commit(new RuleMetadataSaveResult(RuleMetadataSaveOutcome.Success));
            }

            RuleTagEntry[] tags = await context.Set<RuleTagEntry>()
                .Where(tag => values.TagIds.Contains(tag.PublicId))
                .ToArrayAsync(cancellationToken);
            if (tags.Length != values.TagIds.Count)
            {
                return transaction.Rollback(new RuleMetadataSaveResult(RuleMetadataSaveOutcome.TagNotFound));
            }

            if (metadata is null)
            {
                metadata = new RuleMetadataEntry { RuleId = ruleId };
                context.Add(metadata);
            }
            else
            {
                context.RemoveRange(metadata.Tags);
                metadata.Tags.Clear();
            }

            metadata.Notes = values.Notes;
            foreach (RuleTagEntry tag in tags.OrderBy(static tag => tag.Name, StringComparer.OrdinalIgnoreCase))
            {
                metadata.Tags.Add(new RuleMetadataTagEntry
                {
                    RuleMetadata = metadata,
                    Tag = tag,
                });
            }

            await context.SaveChangesAsync(cancellationToken);
            return transaction.Commit(new RuleMetadataSaveResult(RuleMetadataSaveOutcome.Success, ToItem(metadata)));
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

    private static RuleMetadataItem ToItem(RuleMetadataEntry metadata)
    {
        RuleTagItem[] tags = metadata.Tags
            .Select(static relation => relation.Tag)
            .OrderBy(static tag => tag.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static tag => tag.PublicId)
            .Select(static tag => new RuleTagItem(tag.PublicId, tag.Name, tag.Color))
            .ToArray();
        return new RuleMetadataItem(metadata.PublicId, metadata.RuleId, metadata.Notes, tags);
    }
}
