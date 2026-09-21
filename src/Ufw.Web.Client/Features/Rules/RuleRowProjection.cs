using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Features.Rules;

public sealed record RuleRowProjection(
    ListedFirewallRule Rule,
    FirewallAddressFamily AddressFamily,
    int OccurrenceId,
    int FamilyPosition,
    int FamilyCount,
    bool CanOrder,
    bool CanMutate,
    RulePositionChange? PositionChange,
    RuleMetadata? Metadata = null,
    string? CanonicalCommand = null);
