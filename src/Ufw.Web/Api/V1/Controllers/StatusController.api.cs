using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ufw.Web.Api.V1.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/status")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class StatusController
{
    /// <summary>
    /// Probes the ufw-systemd process through the local daemon IPC boundary.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public partial Task<IActionResult> GetStatusAsync(CancellationToken cancellationToken);
}
