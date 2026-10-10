using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.Features.Rules.Metadata;

internal interface IRuleMetadataMutationService
{
    Task<RuleMetadataMutationResponse> UpdateAsync(string ruleId, RuleMetadataChange change, CancellationToken cancellationToken = default);
}
