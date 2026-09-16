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
                .Where(entry => identities.Contains(entry.RuleId))
                .OrderBy(static entry => entry.RuleId)
                .ToArrayAsync(cancellationToken);
            IReadOnlyList<RuleMetadataItem> result = metadata.Select(ToItem).ToArray();
            return result;
        });
    }

    public Task<RuleMetadataItem?> SaveAsync(string ruleId, RuleMetadataValues values, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentNullException.ThrowIfNull(values);

        return Transaction.Scoped.RunAsync<RuleMetadataItem?>(async (context, transaction) =>
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
                return transaction.Commit<RuleMetadataItem?>(null);
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

            metadata.Group = values.Group;
            metadata.Notes = values.Notes;
            foreach (RuleMetadataTagValues tag in values.Tags)
            {
                metadata.Tags.Add(new RuleMetadataTagEntry
                {
                    RuleMetadata = metadata,
                    Name = tag.Name,
                    NormalizedName = tag.NormalizedName,
                });
            }

            await context.SaveChangesAsync(cancellationToken);
            return transaction.Commit<RuleMetadataItem?>(ToItem(metadata));
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
        string[] tags = metadata.Tags
            .OrderBy(static tag => tag.NormalizedName, StringComparer.Ordinal)
            .ThenBy(static tag => tag.Name, StringComparer.Ordinal)
            .Select(static tag => tag.Name)
            .ToArray();
        return new RuleMetadataItem(metadata.RuleId, metadata.Group, metadata.Notes, tags);
    }
}
