using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Api.V1.Models.Auth;

namespace Ufw.Web.Api.V1.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/auth")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class AuthController
{
    /// <summary>
    /// Authenticates a user and issues a short-lived access token plus the refresh-token cookie.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public partial Task<IActionResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Rotates the current refresh token and issues a replacement access token.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public partial Task<IActionResult> RefreshAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Revokes the current refresh-token family and clears the refresh-token cookie.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public partial Task<IActionResult> LogoutAsync(CancellationToken cancellationToken);
}
