using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ufw.Web.Api.V1.Models.Auth;
using Ufw.Web.Configuration;
using Ufw.Web.Services.Auth;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Ufw.Web.Api.V1.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/auth")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AuthController
(
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    IAuthenticationTimingService authenticationTimingService,
    IOptions<RefreshTokenOptions> refreshTokenOptions
) : ControllerBase
{
    private readonly RefreshTokenOptions _refreshTokenOptions = refreshTokenOptions.Value;

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthTokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IdentityUser? user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            authenticationTimingService.PerformDummyPasswordVerification(request.Password);
            return Unauthorized();
        }

        SignInResult result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut || result.IsNotAllowed)
            {
                authenticationTimingService.PerformDummyPasswordVerification(request.Password);
            }
            return Unauthorized();
        }

        AccessToken accessToken = await jwtTokenService.IssueAsync(user, cancellationToken);
        RefreshTokenIssueResult refreshToken = await refreshTokenService.IssueAsync(user, cancellationToken);
        SetRefreshTokenCookie(refreshToken.Token, refreshToken.ExpiresAt);

        return Ok(new AuthTokenResponse(accessToken.Value, accessToken.ExpiresAt));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthTokenResponse>> RefreshAsync(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(_refreshTokenOptions.CookieName, out string? refreshToken)
            || string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized();
        }

        RefreshTokenRotationResult? rotation = await refreshTokenService.RotateAsync(refreshToken, cancellationToken);
        if (rotation is null)
        {
            DeleteRefreshTokenCookie();
            return Unauthorized();
        }

        bool canSignIn = await signInManager.CanSignInAsync(rotation.User);
        bool isLockedOut = userManager.SupportsUserLockout && await userManager.IsLockedOutAsync(rotation.User);
        if (!canSignIn || isLockedOut)
        {
            await refreshTokenService.RevokeFamilyAsync(rotation.Token, cancellationToken);
            DeleteRefreshTokenCookie();
            return Unauthorized();
        }

        AccessToken accessToken = await jwtTokenService.IssueAsync(rotation.User, cancellationToken);
        SetRefreshTokenCookie(rotation.Token, rotation.ExpiresAt);
        return Ok(new AuthTokenResponse(accessToken.Value, accessToken.ExpiresAt));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue(_refreshTokenOptions.CookieName, out string? refreshToken)
            && !string.IsNullOrWhiteSpace(refreshToken))
        {
            await refreshTokenService.RevokeFamilyAsync(refreshToken, cancellationToken);
        }

        DeleteRefreshTokenCookie();
        return NoContent();
    }

    private void SetRefreshTokenCookie(string token, DateTimeOffset expiresAt) => Response.Cookies.Append(
        _refreshTokenOptions.CookieName,
        token,
        new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = expiresAt,
            IsEssential = true,
        });

    private void DeleteRefreshTokenCookie() => Response.Cookies.Delete(
        _refreshTokenOptions.CookieName,
        new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
        });
}
