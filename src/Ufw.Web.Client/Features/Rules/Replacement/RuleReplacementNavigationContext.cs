using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Replacement;

internal sealed record RuleReplacementNavigationContext(
    string BaselineFingerprint,
    int TargetOccurrenceId,
    int TargetFamilyPosition,
    string OriginalRuleId,
    FirewallAddressFamily AddressFamily,
    ListedFirewallRule Target);
