using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Api.V1.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/intent")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class IntentController
{
    /// <summary>
    /// Returns the daemon deployment identity and signed-intent protocol version required for browser-side signing.
    /// </summary>
    [HttpGet("context")]
    [ProducesResponseType<IntentContextResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public partial Task<ActionResult<IntentContextResponse>> GetContextAsync(CancellationToken cancellationToken);
}
