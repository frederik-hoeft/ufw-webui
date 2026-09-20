using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules.Filtering.Actions;

internal sealed record ActionRuleFilter(FirewallAction Action) : RuleFilter;
