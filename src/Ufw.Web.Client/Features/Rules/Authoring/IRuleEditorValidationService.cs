using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Authoring;

internal interface IRuleEditorValidationService
{
    IReadOnlyList<string> Validate(FirewallRuleSpecification specification, string propertyName, bool ipv6Enabled);
}
