using Ufw.Web.Client.Api.Rules;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.Features.Rules;

internal sealed class RuleInventoryService(IRuleApiClient apiClient) : IRuleInventoryService
{
    public async Task<RuleSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        RuleInventoryResponse response = await apiClient.GetInventoryAsync(cancellationToken);
        return RuleSnapshotFactory.FromInventoryResponse(response);
    }
}
