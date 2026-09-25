using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Model.V1.Auth;

namespace Ufw.Web.Api.V1.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/auth")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class AuthController
{
    /// <summary>
    /// Issues the ASP.NET Core antiforgery request token used by cookie-authenticated auth operations.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("antiforgery")]
    [ProducesResponseType<AntiforgeryTokenResponse>(StatusCodes.Status200OK)]
    public partial IActionResult GetAntiforgeryToken();

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
    [ValidateAntiForgeryToken]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public partial Task<IActionResult> RefreshAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Changes the authenticated user's password and rotates the current authentication session.
    /// </summary>
    [Authorize]
    [HttpPost("password")]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public partial Task<IActionResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes the current refresh-token family and clears the refresh-token cookie.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public partial Task<IActionResult> LogoutAsync(CancellationToken cancellationToken);
}
