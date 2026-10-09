using Ufw.Shared.Firewall;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Api.RuleTemplates;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Model.V1.RuleTemplates;

namespace Ufw.Web.Client.Features.Rules.Templates;

internal sealed class RuleTemplateCatalogService(IRuleTemplateApiClient apiClient) : IRuleTemplateCatalogService
{
    public IReadOnlyList<RuleTemplate> Current { get; private set; } = [];

    public long Version { get; private set; }

    public async Task<IReadOnlyList<RuleTemplate>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        RuleTemplateInventoryResponse response = await apiClient.GetAsync(cancellationToken);
        Current = Normalize(response);
        return Current;
    }

    public async Task<IReadOnlyList<RuleTemplate>> CreateAsync(RuleTemplateDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        CreateRuleTemplateRequest request = CreateRequest(definition);
        RuleTemplateInventoryResponse response = await apiClient.CreateAsync(request, cancellationToken);
        Current = Normalize(response);
        Version++;
        return Current;
    }

    public async Task<IReadOnlyList<RuleTemplate>> UpdateAsync(Guid templateId, RuleTemplateDefinition definition, CancellationToken cancellationToken = default)
    {
        if (templateId == Guid.Empty)
        {
            throw new ArgumentException("Rule template ID must not be empty.", nameof(templateId));
        }
        ArgumentNullException.ThrowIfNull(definition);
        UpdateRuleTemplateRequest request = UpdateRequest(definition);
        RuleTemplateInventoryResponse response = await apiClient.UpdateAsync(templateId, request, cancellationToken);
        Current = Normalize(response);
        Version++;
        return Current;
    }

    public async Task<IReadOnlyList<RuleTemplate>> DeleteAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        if (templateId == Guid.Empty)
        {
            throw new ArgumentException("Rule template ID must not be empty.", nameof(templateId));
        }
        RuleTemplateInventoryResponse response = await apiClient.DeleteAsync(templateId, cancellationToken);
        Current = Normalize(response);
        Version++;
        return Current;
    }

    private static CreateRuleTemplateRequest CreateRequest(RuleTemplateDefinition definition) => new()
    {
        Name = definition.Name,
        Description = definition.Description,
        Rule = definition.Rule,
        Notes = definition.Notes,
        TagIds = definition.TagIds,
        GroupId = definition.GroupId,
    };

    private static UpdateRuleTemplateRequest UpdateRequest(RuleTemplateDefinition definition) => new()
    {
        Name = definition.Name,
        Description = definition.Description,
        Rule = definition.Rule,
        Notes = definition.Notes,
        TagIds = definition.TagIds,
        GroupId = definition.GroupId,
    };

    private static IReadOnlyList<RuleTemplate> Normalize(RuleTemplateInventoryResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.Templates is null)
        {
            throw new ApiProtocolException("Rule-template inventory response is missing the template list.");
        }

        List<RuleTemplate> templates = new(response.Templates.Count);
        foreach (RuleTemplateItem item in response.Templates)
        {
            templates.Add(Normalize(item));
        }

        if (templates.Select(static template => template.Id).Distinct().Count() != templates.Count)
        {
            throw new ApiProtocolException("Rule-template inventory response contains duplicate template identities.");
        }

        return templates
            .OrderBy(static template => template.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static template => template.Name, StringComparer.Ordinal)
            .ThenBy(static template => template.Id)
            .ToArray();
    }

    private static RuleTemplate Normalize(RuleTemplateItem item)
    {
        if (item is null || item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Name) || item.Rule is null || item.Tags is null)
        {
            throw new ApiProtocolException("Rule-template inventory response contains an invalid template entry.");
        }

        string name = item.Name.Trim();
        string? description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim();
        string? notes = string.IsNullOrWhiteSpace(item.Notes) ? null : item.Notes.Trim();
        if (name.Length > RuleTemplateLimits.MAX_NAME_LENGTH
            || description?.Length > RuleTemplateLimits.MAX_DESCRIPTION_LENGTH
            || notes?.Length > RuleMetadataLimits.MAX_NOTES_LENGTH)
        {
            throw new ApiProtocolException("Rule-template inventory response contains invalid text values.");
        }

        FirewallRuleSpecification rule = RuleSpecificationNormalizer.Normalize(item.Rule);
        if (RuleSpecificationValidator.Validate(rule).Length != 0)
        {
            throw new ApiProtocolException("Rule-template inventory response contains an invalid rule definition.");
        }

        RuleTag[] tags = [.. item.Tags.Select(tag => RuleMetadataProtocolMapper.MapTag(tag, "Rule-template inventory response contains an invalid tag entry."))];
        if (tags.Select(static tag => tag.Id).Distinct().Count() != tags.Length
            || tags.Select(static tag => tag.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != tags.Length)
        {
            throw new ApiProtocolException("Rule-template inventory response contains duplicate tag identities.");
        }

        RuleGroupMembership? group = RuleMetadataProtocolMapper.MapGroupReference(item.Group, "Rule-template inventory response contains an invalid group reference.");
        return new RuleTemplate(
            item.Id,
            name,
            description,
            rule,
            notes,
            [.. tags.OrderBy(static tag => tag.Name, StringComparer.OrdinalIgnoreCase).ThenBy(static tag => tag.Name, StringComparer.Ordinal)],
            group);
    }
}
