using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Model.V1.RuleMetadata;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class RuleMetadataController(IRuleMetadataReconciliationService reconciliation) : ControllerBase
{
    public async partial Task<ActionResult<RuleMetadataReconciliationResponse>> GetReconciliationAsync(CancellationToken cancellationToken) =>
        Ok(await reconciliation.GetAsync(cancellationToken));

    public async partial Task<ActionResult<RuleMetadataReconciliationResponse>> CleanupAsync(CleanupRuleMetadataRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MetadataIds is null || request.MetadataIds.Count == 0 || request.MetadataIds.Any(static id => id == Guid.Empty))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Rule metadata cleanup selection is invalid",
                Detail = "Select at least one valid orphaned metadata identity to remove.",
            });
        }

        return Ok(await reconciliation.CleanupAsync(request, cancellationToken));
    }
}
