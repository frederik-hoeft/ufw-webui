using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ufw.Web.Api.V1.Models.Auth;
using Ufw.Web.Configuration;
using Ufw.Web.Services.Auth;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class AuthController(IAuthenticationFlowService authenticationFlowService, IOptions<RefreshTokenOptions> refreshTokenOptions) : ControllerBase
{
    private readonly RefreshTokenOptions _refreshTokenOptions = refreshTokenOptions.Value;

    public async partial Task<IActionResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        AuthenticationTokenResult? result = await authenticationFlowService.LoginAsync(request.Email, request.Password, cancellationToken);
        if (result is null)
        {
            return Unauthorized();
        }

        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
        return Ok(new AuthTokenResponse(result.AccessToken.Value, result.AccessToken.ExpiresAt));
    }

    public async partial Task<IActionResult> RefreshAsync(CancellationToken cancellationToken)
    {
        if (!TryGetRefreshToken(out string? refreshToken))
        {
            return Unauthorized();
        }

        AuthenticationTokenResult? result = await authenticationFlowService.RefreshAsync(refreshToken!, cancellationToken);
        if (result is null)
        {
            DeleteRefreshTokenCookie();
            return Unauthorized();
        }

        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
        return Ok(new AuthTokenResponse(result.AccessToken.Value, result.AccessToken.ExpiresAt));
    }

    public async partial Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        if (TryGetRefreshToken(out string? refreshToken))
        {
            await authenticationFlowService.RevokeAsync(refreshToken!, cancellationToken);
        }

        DeleteRefreshTokenCookie();
        return NoContent();
    }

    private bool TryGetRefreshToken(out string? refreshToken) =>
        Request.Cookies.TryGetValue(_refreshTokenOptions.CookieName, out refreshToken) && !string.IsNullOrWhiteSpace(refreshToken);

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
