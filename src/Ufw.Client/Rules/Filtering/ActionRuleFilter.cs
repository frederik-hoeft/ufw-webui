using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules.Filtering;

internal sealed record ActionRuleFilter(FirewallAction Action) : RuleFilter;
