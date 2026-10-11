using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Client.Features.Rules;

internal sealed record RuleListOrderingResult(
    RuleInventoryState State,
    RuleReorderResponse Response,
    IReadOnlyList<ListedFirewallRule> BaselineRules,
    IReadOnlyList<int> DesiredOrder);
