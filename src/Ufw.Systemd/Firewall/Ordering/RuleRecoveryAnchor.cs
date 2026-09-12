using Ufw.Shared.Firewall;

namespace Ufw.Systemd.Firewall.Ordering;

internal sealed record RuleRecoveryAnchor(FirewallRuleSpecification? Rule, string? RawLine);
