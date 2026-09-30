using Microsoft.EntityFrameworkCore;
using Ufw.Shared.Firewall;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Ufw.Web.Model.V1.RuleGroups;
using Ufw.Web.Model.V1.RuleTags;
using Ufw.Web.Model.V1.RuleTemplates;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;

namespace Ufw.Web.Services.Rules;

internal sealed class RuleTemplateRepository(ITransactionServiceHandle transactionService)
    : DatabaseService<ApplicationDbContext>(transactionService), IRuleTemplateRepository
{
    public Task<RuleTemplateInventoryResponse> GetAsync(CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunReadOnlyAsync(context => GetCoreAsync(context, cancellationToken));

    public Task<RuleTemplateMutationResult> CreateAsync(RuleTemplateValues values, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<RuleTemplateMutationResult>(async (context, transaction) =>
        {
            TemplateDependencies dependencies = await ResolveDependenciesAsync(context, values.Metadata, cancellationToken);
            if (dependencies.Outcome is { } dependencyFailure)
            {
                return transaction.Rollback(new RuleTemplateMutationResult(dependencyFailure));
            }

            RuleTemplateEntry template = CreateEntry(values, dependencies);
            context.Add(template);
            await context.SaveChangesAsync(cancellationToken);

            RuleTemplateInventoryResponse inventory = await GetCoreAsync(context, cancellationToken);
            return transaction.Commit(new RuleTemplateMutationResult(RuleTemplateMutationOutcome.Success, inventory));
        });

    public Task<RuleTemplateMutationResult> UpdateAsync(Guid publicId, RuleTemplateValues values, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<RuleTemplateMutationResult>(async (context, transaction) =>
        {
            RuleTemplateEntry? template = await context.Set<RuleTemplateEntry>()
                .Include(static candidate => candidate.Tags)
                .SingleOrDefaultAsync(candidate => candidate.PublicId == publicId, cancellationToken);
            if (template is null)
            {
                return transaction.Rollback(new RuleTemplateMutationResult(RuleTemplateMutationOutcome.NotFound));
            }
            TemplateDependencies dependencies = await ResolveDependenciesAsync(context, values.Metadata, cancellationToken);
            if (dependencies.Outcome is { } dependencyFailure)
            {
                return transaction.Rollback(new RuleTemplateMutationResult(dependencyFailure));
            }

            ApplyValues(template, values, dependencies.Group);
            SynchronizeTags(context, template, dependencies.Tags);
            await context.SaveChangesAsync(cancellationToken);

            RuleTemplateInventoryResponse inventory = await GetCoreAsync(context, cancellationToken);
            return transaction.Commit(new RuleTemplateMutationResult(RuleTemplateMutationOutcome.Success, inventory));
        });

    public Task<RuleTemplateMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<RuleTemplateMutationResult>(async (context, transaction) =>
        {
            RuleTemplateEntry? template = await context.Set<RuleTemplateEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == publicId, cancellationToken);
            if (template is null)
            {
                return transaction.Rollback(new RuleTemplateMutationResult(RuleTemplateMutationOutcome.NotFound));
            }

            context.Remove(template);
            await context.SaveChangesAsync(cancellationToken);
            RuleTemplateInventoryResponse inventory = await GetCoreAsync(context, cancellationToken);
            return transaction.Commit(new RuleTemplateMutationResult(RuleTemplateMutationOutcome.Success, inventory));
        });

    private static RuleTemplateEntry CreateEntry(RuleTemplateValues values, TemplateDependencies dependencies)
    {
        RuleTemplateEntry template = new()
        {
            Name = values.Name,
            Description = values.Description,
            Group = dependencies.Group,
            Notes = values.Metadata.Notes,
            Source = RuleSpecificationNormalizer.ANY,
            Destination = RuleSpecificationNormalizer.ANY,
        };
        ApplyValues(template, values, dependencies.Group);
        template.Tags = [.. dependencies.Tags.Select(tag => new RuleTemplateTagEntry { RuleTemplate = template, Tag = tag })];
        return template;
    }

    private static void ApplyValues(RuleTemplateEntry template, RuleTemplateValues values, RuleGroupEntry? group)
    {
        FirewallRuleSpecification rule = values.Rule;
        template.Name = values.Name;
        template.Description = values.Description;
        template.Action = rule.Action;
        template.AddressFamily = rule.AddressFamily;
        template.Direction = rule.Direction;
        template.Protocol = rule.Protocol;
        template.Source = rule.Source ?? RuleSpecificationNormalizer.ANY;
        template.SourcePorts = rule.SourcePorts;
        template.SourceInterface = rule.SourceInterface;
        template.Destination = rule.Destination ?? RuleSpecificationNormalizer.ANY;
        template.DestinationPorts = rule.DestinationPorts;
        template.DestinationInterface = rule.DestinationInterface;
        template.Comment = rule.Comment;
        template.Notes = values.Metadata.Notes;
        template.Group = group;
        template.GroupId = group?.Id;
    }

    private static void SynchronizeTags(ApplicationDbContext context, RuleTemplateEntry template, IReadOnlyList<RuleTagEntry> desiredTags)
    {
        HashSet<long> desiredTagIds = [.. desiredTags.Select(static tag => tag.Id)];
        RuleTemplateTagEntry[] removed = [.. template.Tags.Where(relation => !desiredTagIds.Contains(relation.TagId))];
        context.RemoveRange(removed);

        HashSet<long> existingTagIds = [.. template.Tags.Select(static relation => relation.TagId)];
        foreach (RuleTagEntry tag in desiredTags.Where(tag => !existingTagIds.Contains(tag.Id)))
        {
            template.Tags.Add(new RuleTemplateTagEntry { RuleTemplate = template, Tag = tag });
        }
    }

    private static async Task<TemplateDependencies> ResolveDependenciesAsync(ApplicationDbContext context, RuleMetadataValues metadata, CancellationToken cancellationToken)
    {
        RuleTagEntry[] tags = metadata.TagIds.Count == 0
            ? []
            : await context.Set<RuleTagEntry>().Where(tag => metadata.TagIds.Contains(tag.PublicId)).ToArrayAsync(cancellationToken);
        if (tags.Length != metadata.TagIds.Count)
        {
            return new TemplateDependencies(RuleTemplateMutationOutcome.TagNotFound, [], Group: null);
        }

        RuleGroupEntry? group = null;
        if (metadata.GroupId is { } groupId)
        {
            group = await context.Set<RuleGroupEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == groupId, cancellationToken);
            if (group is null)
            {
                return new TemplateDependencies(RuleTemplateMutationOutcome.GroupNotFound, [], Group: null);
            }
        }

        return new TemplateDependencies(Outcome: null, [.. tags.OrderBy(static tag => tag.PublicId)], group);
    }

    private static async Task<RuleTemplateInventoryResponse> GetCoreAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        RuleTemplateEntry[] templates = await context.Set<RuleTemplateEntry>()
            .AsNoTracking()
            .Include(static template => template.Tags)
                .ThenInclude(static relation => relation.Tag)
            .Include(static template => template.Group)
            .OrderBy(static template => template.Name)
            .ThenBy(static template => template.PublicId)
            .ToArrayAsync(cancellationToken);

        RuleTemplateItem[] items = [.. templates.Select(static template => new RuleTemplateItem
        {
            Id = template.PublicId,
            Name = template.Name,
            Description = template.Description,
            Rule = new FirewallRuleSpecification
            {
                Action = template.Action,
                AddressFamily = template.AddressFamily,
                Direction = template.Direction,
                Protocol = template.Protocol,
                Source = template.Source,
                SourcePorts = template.SourcePorts,
                SourceInterface = template.SourceInterface,
                Destination = template.Destination,
                DestinationPorts = template.DestinationPorts,
                DestinationInterface = template.DestinationInterface,
                Comment = template.Comment,
            },
            Notes = template.Notes,
            Tags = [.. template.Tags.Select(static relation => relation.Tag).OrderBy(static tag => tag.Name).ThenBy(static tag => tag.PublicId)
                .Select(static tag => new RuleTagItem(tag.PublicId, tag.Name, tag.Color))],
            Group = template.Group is null ? null : new RuleGroupSummary(template.Group.PublicId, template.Group.Name, template.Group.Comment),
        })];
        return new RuleTemplateInventoryResponse(items);
    }

    private sealed record TemplateDependencies(RuleTemplateMutationOutcome? Outcome, IReadOnlyList<RuleTagEntry> Tags, RuleGroupEntry? Group);
}
