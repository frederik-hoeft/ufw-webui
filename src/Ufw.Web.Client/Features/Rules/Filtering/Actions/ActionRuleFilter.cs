using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Filtering.Actions;

internal sealed record ActionRuleFilter(FirewallAction Action) : RuleFilter;
