using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Client.Api;

internal interface IRuleOrderingService
{
    Task<RuleReorderResponse> ApplyAsync(
        RuleListResponse baseline,
        IReadOnlyList<int> desiredOrder,
        string privateKey,
        CancellationToken cancellationToken = default);
}
