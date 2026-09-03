using Ufw.Ipc.Shared.Model.Domain.Rules;

namespace Ufw.Ipc.Shared.Model.Responses.Domain;

public sealed record RuleMutationResponse(string Operation, ListedFirewallRule Rule) : OkResponseBase;
