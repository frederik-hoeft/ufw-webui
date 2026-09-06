using Ufw.Shared.Firewall;

namespace Ufw.Shared.Ipc.Model.Responses.Domain;

public sealed record RuleMutationResponse(string Operation, ListedFirewallRule Rule) : OkResponseBase;
