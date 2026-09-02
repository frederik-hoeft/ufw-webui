using Ufw.Ipc.Shared.Model.Domain.Rules;

namespace Ufw.Ipc.Shared.Model.Responses.Domain;

public sealed record RuleListResponse(bool Active, IReadOnlyList<ListedFirewallRule> Rules) : OkResponseBase;
