using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Filtering.Protocols;

internal sealed record ProtocolRuleFilter(FirewallProtocol Protocol) : RuleFilter;
