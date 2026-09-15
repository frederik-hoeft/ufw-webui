using Ufw.Shared.Firewall;

namespace Ufw.Client.RuleInsertion;

internal sealed record OrderedRuleInsertionNavigationContext(
    string BaselineFingerprint,
    int AnchorOccurrenceId,
    int AnchorFamilyPosition,
    RuleInsertionPlacement Placement,
    FirewallAddressFamily AddressFamily,
    ListedFirewallRule Anchor);
