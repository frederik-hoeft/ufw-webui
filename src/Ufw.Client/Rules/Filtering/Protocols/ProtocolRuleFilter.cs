using Ufw.Client.Rules.Filtering;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules.Filtering.Protocols;

internal sealed record ProtocolRuleFilter(FirewallProtocol Protocol) : RuleFilter;
