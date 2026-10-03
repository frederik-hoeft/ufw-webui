using Microsoft.EntityFrameworkCore;
using Ufw.Shared.Firewall;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Data.Extensions;
using Ufw.Web.Data.Model;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;

namespace Ufw.Web.Data.Access.Rules.Templates;

internal sealed class RuleTemplateDataAccess(ITransactionServiceHandle transactionService)
    : DatabaseService<ApplicationDbContext>(transactionService), IRuleTemplateDataAccess
{
    public Task<IReadOnlyList<RuleTemplateItem>> GetAsync(CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunReadOnlyAsync(context => GetCoreAsync(context, cancellationToken));

    public Task<DataMutationResult> CreateAsync(RuleTemplateValues values, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync(async (context, transaction) =>
        {
            TemplateDependencies dependencies = await ResolveDependenciesAsync(context, values, cancellationToken);
            if (dependencies.Error is { } dependencyError)
            {
                return transaction.Rollback(DataMutationResult.Failure(dependencyError));
            }

            context.Add(CreateEntry(values, dependencies));
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

    public Task<DataMutationResult> UpdateAsync(Guid publicId, RuleTemplateValues values, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync(async (context, transaction) =>
        {
            RuleTemplateEntry? template = await context.Set<RuleTemplateEntry>()
                .Include(static candidate => candidate.Tags)
                .SingleOrDefaultAsync(candidate => candidate.PublicId == publicId, cancellationToken);
            if (template is null)
            {
                return transaction.Rollback(DataMutationResult.Failure(new DataMutationNotFoundError()));
            }

            TemplateDependencies dependencies = await ResolveDependenciesAsync(context, values, cancellationToken);
            if (dependencies.Error is { } dependencyError)
            {
                return transaction.Rollback(DataMutationResult.Failure(dependencyError));
            }

            ApplyValues(template, values, dependencies.Group);
            SynchronizeTags(context, template, dependencies.Tags);
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

    public Task<DataMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync(async (context, transaction) =>
        {
            RuleTemplateEntry? template = await context.Set<RuleTemplateEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == publicId, cancellationToken);
            if (template is null)
            {
                return transaction.Rollback(DataMutationResult.Failure(new DataMutationNotFoundError()));
            }

            context.Remove(template);
            await context.SaveChangesAsync(cancellationToken);
            return transaction.Commit(DataMutationResult.Success());
        });

    private static RuleTemplateEntry CreateEntry(RuleTemplateValues values, TemplateDependencies dependencies)
    {
        RuleTemplateEntry template = new()
        {
            Name = values.Name,
            Description = values.Description,
            Group = dependencies.Group,
            Notes = values.Notes,
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
        template.Notes = values.Notes;
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

    private static async Task<TemplateDependencies> ResolveDependenciesAsync(ApplicationDbContext context, RuleTemplateValues values, CancellationToken cancellationToken)
    {
        RuleTagEntry[] tags;
        if (values.TagIds.Count == 0)
        {
            tags = [];
        }
        else
        {
            tags = await context.Set<RuleTagEntry>().Where(tag => values.TagIds.Contains(tag.PublicId)).ToArrayAsync(cancellationToken);
        }
        if (tags.Length != values.TagIds.Count)
        {
            HashSet<Guid> resolvedTagIds = [.. tags.Select(static tag => tag.PublicId)];
            Guid[] missingTagIds = [.. values.TagIds.Where(tagId => !resolvedTagIds.Contains(tagId))];
            return new TemplateDependencies(new RuleTagsNotFoundError(missingTagIds), [], Group: null);
        }

        RuleGroupEntry? group = null;
        if (values.GroupId is { } groupId)
        {
            group = await context.Set<RuleGroupEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == groupId, cancellationToken);
            if (group is null)
            {
                return new TemplateDependencies(new RuleGroupNotFoundError(groupId), [], Group: null);
            }
        }

        return new TemplateDependencies(Error: null, [.. tags.OrderBy(static tag => tag.PublicId)], group);
    }

    private static async Task<IReadOnlyList<RuleTemplateItem>> GetCoreAsync(ApplicationDbContext context, CancellationToken cancellationToken) =>
        await context.Set<RuleTemplateEntry>()
            .AsNoTracking()
            .OrderBy(static template => template.Name)
            .ThenBy(static template => template.PublicId)
            .Select(static template => new RuleTemplateItem
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
                Tags = template.Tags
                    .Select(static relation => relation.Tag)
                    .OrderBy(static tag => tag.Name)
                    .ThenBy(static tag => tag.PublicId)
                    .Select(static tag => new RuleTagItem(tag.PublicId, tag.Name, tag.Color))
                    .ToList(),
                Group = template.Group == null ? null : new RuleGroupSummary(template.Group.PublicId, template.Group.Name, template.Group.Comment),
            })
            .ToListAsync(cancellationToken);

    private sealed record TemplateDependencies(DataMutationError? Error, IReadOnlyList<RuleTagEntry> Tags, RuleGroupEntry? Group);
}
