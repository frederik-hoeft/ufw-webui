using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Model.V1.RuleMetadata;

namespace Ufw.Web.Api.V1.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/rule-metadata")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class RuleMetadataController
{
    /// <summary>
    /// Reports application-owned rule metadata that no longer matches an authoritative UFW rule identity.
    /// </summary>
    [HttpGet("reconciliation")]
    [ProducesResponseType<RuleMetadataReconciliationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public partial Task<ActionResult<RuleMetadataReconciliationResponse>> GetReconciliationAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Removes reviewed metadata records that are still unmatched against the current authoritative UFW snapshot.
    /// </summary>
    [HttpPost("reconciliation/cleanup")]
    [ProducesResponseType<RuleMetadataReconciliationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public partial Task<ActionResult<RuleMetadataReconciliationResponse>> CleanupAsync([FromBody] CleanupRuleMetadataRequest request, CancellationToken cancellationToken);
}
