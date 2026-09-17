using Ufw.Web.Api.V1.Models.Rules;

namespace Ufw.Web.Services.Rules;

public interface IRuleMetadataService
{
    Task<RuleMetadataUpdateResult> UpdateAsync(string ruleId, UpdateRuleMetadataRequest request, CancellationToken cancellationToken = default);

    Task RemoveForDeletedRuleAsync(string ruleId, CancellationToken cancellationToken = default);
}
