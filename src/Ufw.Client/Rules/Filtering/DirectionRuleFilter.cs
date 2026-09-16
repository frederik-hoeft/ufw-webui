using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules.Filtering;

internal sealed record DirectionRuleFilter(FirewallDirection Direction) : RuleFilter;
