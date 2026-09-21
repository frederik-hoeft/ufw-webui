using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Filtering.Directions;

internal sealed record DirectionRuleFilter(FirewallDirection Direction) : RuleFilter;
