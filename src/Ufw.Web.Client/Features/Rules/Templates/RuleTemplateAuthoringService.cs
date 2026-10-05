using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Authoring;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Features.Rules.Templates;

internal sealed class RuleTemplateAuthoringService(IRuleDraftFactory ruleDraftFactory) : IRuleTemplateAuthoringService
{
    public RuleTemplateDefinition CreateDefinition(string name, string? description, FirewallRuleSpecification rule, RuleMetadata? metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(rule);
        RuleMetadata? sourceMetadata = metadata;
        return new RuleTemplateDefinition(
            name.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            ruleDraftFactory.CreateFromExisting(rule),
            string.IsNullOrWhiteSpace(sourceMetadata?.Notes) ? null : sourceMetadata.Notes.Trim(),
            sourceMetadata?.Tags.Select(static tag => tag.Id).Distinct().Order().ToArray() ?? [],
            sourceMetadata?.Group?.Id);
    }

    public RuleTemplateInstantiationResult Initialize(RuleTemplate ruleTemplate, FirewallAddressFamily? requiredFamily = null)
    {
        ArgumentNullException.ThrowIfNull(ruleTemplate);
        FirewallRuleSpecification rule = ruleDraftFactory.CreateFromExisting(ruleTemplate.Rule);
        if (requiredFamily is { } family)
        {
            if (family is not FirewallAddressFamily.IPv4 and not FirewallAddressFamily.IPv6)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredFamily), requiredFamily, "Required rule family must be concrete.");
            }
            if (rule.AddressFamily is FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6 && rule.AddressFamily != family)
            {
                return RuleTemplateInstantiationResult.Failure(RuleTemplateInstantiationError.AddressFamilyMismatch);
            }
            rule.AddressFamily = family;
        }

        return RuleTemplateInstantiationResult.Success(new RuleTemplateInstantiation(
            rule,
            ruleTemplate.Notes,
            ruleTemplate.TagIds.ToArray(),
            ruleTemplate.GroupId));
    }
}
