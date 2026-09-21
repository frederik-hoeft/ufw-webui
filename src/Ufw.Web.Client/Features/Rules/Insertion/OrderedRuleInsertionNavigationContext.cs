using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Insertion;

internal sealed record OrderedRuleInsertionNavigationContext(
    string BaselineFingerprint,
    int AnchorOccurrenceId,
    int AnchorFamilyPosition,
    RuleInsertionPlacement Placement,
    FirewallAddressFamily AddressFamily,
    ListedFirewallRule Anchor);
