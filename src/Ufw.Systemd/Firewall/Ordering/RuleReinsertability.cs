using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;

namespace Ufw.Systemd.Firewall.Ordering;

internal sealed record RuleReinsertability(
    bool IsReinsertable,
    string? Reason,
    FirewallRuleSpecification? Specification,
    UfwRenderedRule? RenderedRule,
    int KeepPriority);
