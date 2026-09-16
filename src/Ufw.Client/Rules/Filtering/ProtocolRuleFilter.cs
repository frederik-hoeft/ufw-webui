using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules.Filtering;

internal sealed record ProtocolRuleFilter(FirewallProtocol Protocol) : RuleFilter;
