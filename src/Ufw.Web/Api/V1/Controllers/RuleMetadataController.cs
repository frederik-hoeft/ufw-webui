using Microsoft.AspNetCore.Mvc;
using Ufw.Ipc.Client;
using Ufw.Web.Api.V1.Errors;
using Ufw.Web.Api.V1.Models.Rules;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class RuleMetadataController(IRuleMetadataReconciliationService reconciliation, IDaemonApiErrorMapper daemonErrors) : ControllerBase
{
    public async partial Task<ActionResult<RuleMetadataReconciliationResponse>> GetReconciliationAsync(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await reconciliation.GetAsync(cancellationToken));
        }
        catch (UfwIpcException exception)
        {
            return MapDaemonError(exception);
        }
    }

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

        try
        {
            return Ok(await reconciliation.CleanupAsync(request, cancellationToken));
        }
        catch (UfwIpcException exception)
        {
            return MapDaemonError(exception);
        }
    }

    private ObjectResult MapDaemonError(UfwIpcException exception)
    {
        DaemonApiError error = daemonErrors.MapProxyFailure(exception);
        return StatusCode(error.StatusCode, error.Problem);
    }
}
