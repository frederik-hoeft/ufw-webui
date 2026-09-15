using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules.Authoring;

internal interface IRuleEditorValidationService
{
    IReadOnlyList<string> Validate(FirewallRuleSpecification specification, string propertyName, bool ipv6Enabled);
}
