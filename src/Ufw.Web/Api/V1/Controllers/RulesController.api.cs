using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Api.V1.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/rules")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class RulesController
{
    /// <summary>
    /// Returns the daemon-authoritative firewall rule snapshot.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<RuleListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public partial Task<ActionResult<RuleListResponse>> GetRulesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Forwards an administrator-signed add-rule intent to the privileged daemon.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<RuleMutationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public partial Task<ActionResult<RuleMutationResponse>> AddRuleAsync([FromBody] AddRuleRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Forwards an administrator-signed delete-rule intent to the privileged daemon.
    /// </summary>
    [HttpDelete]
    [ProducesResponseType<RuleMutationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public partial Task<ActionResult<RuleMutationResponse>> DeleteRuleAsync([FromBody] DeleteRuleRequest request, CancellationToken cancellationToken);
}
