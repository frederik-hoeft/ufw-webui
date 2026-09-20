using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules.Filtering.Directions;

internal sealed record DirectionRuleFilter(FirewallDirection Direction) : RuleFilter;
