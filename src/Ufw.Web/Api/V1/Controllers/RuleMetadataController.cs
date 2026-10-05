using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Model.V1.RuleMetadata;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class RuleMetadataController(IRuleMetadataReconciliationService reconciliation) : ControllerBase
{
    public async partial Task<ActionResult<RuleMetadataReconciliationResponse>> GetReconciliationAsync(CancellationToken cancellationToken)
    {
        RuleMetadataReconciliationResponse response = await reconciliation.GetAsync(cancellationToken);
        return Ok(response);
    }

    public async partial Task<ActionResult<RuleMetadataReconciliationResponse>> CleanupAsync(CleanupRuleMetadataRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuleMetadataReconciliationResponse response = await reconciliation.CleanupAsync(request, cancellationToken);
        return Ok(response);
    }
}
