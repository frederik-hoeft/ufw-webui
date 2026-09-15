using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules;

public sealed record RuleRowProjection(
    ListedFirewallRule Rule,
    FirewallAddressFamily AddressFamily,
    int OccurrenceId,
    int FamilyPosition,
    int FamilyCount,
    bool CanOrder,
    bool CanMutate,
    RulePositionChange? PositionChange);
