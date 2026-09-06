using Ufw.Shared.Firewall;

namespace Ufw.Shared.Ipc.Model.Responses.Domain;

public sealed record RuleListResponse(bool Active, IReadOnlyList<ListedFirewallRule> Rules) : OkResponseBase;
