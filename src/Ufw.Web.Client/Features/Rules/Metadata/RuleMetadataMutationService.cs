using Ufw.Web.Client.Api;
using Ufw.Web.Client.Api.Rules;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.Features.Rules.Metadata;

internal sealed class RuleMetadataMutationService(IRuleApiClient apiClient) : IRuleMetadataMutationService
{
    public async Task<RuleMetadataMutationResponse> UpdateAsync(string ruleId, RuleMetadataChange change, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentNullException.ThrowIfNull(change);
        RuleMetadataMutationResponse response = await apiClient.UpdateMetadataAsync(ruleId, new UpdateRuleMetadataRequest
        {
            Notes = change.Notes,
            TagIds = change.TagIds,
            GroupId = change.GroupId,
        }, cancellationToken);
        if (response.Metadata is not null)
        {
            if (!string.Equals(response.Metadata.RuleId, ruleId, StringComparison.Ordinal))
            {
                throw new ApiProtocolException("The metadata mutation response refers to a different rule identity.");
            }
            _ = RuleMetadataProtocolMapper.MapMetadata(response.Metadata, "The metadata mutation response contains invalid metadata.");
        }
        return response;
    }
}
