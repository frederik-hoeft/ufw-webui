using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using Ufw.Web.Configuration;
using Ufw.Web.Model.V1.Auth;
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

    public async partial Task<IActionResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        PasswordChangeResult? result = await authenticationFlowService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, cancellationToken);
        if (result is null)
        {
            DeleteRefreshTokenCookie();
            return Unauthorized();
        }

        if (!result.IdentityResult.Succeeded)
        {
            Dictionary<string, string[]> errors = result.IdentityResult.Errors
                .GroupBy(static error => string.Equals(error.Code, "PasswordMismatch", StringComparison.Ordinal)
                    ? nameof(ChangePasswordRequest.CurrentPassword)
                    : nameof(ChangePasswordRequest.NewPassword))
                .ToDictionary(
                    static group => group.Key,
                    static group => group.Select(static error => error.Description).ToArray(),
                    StringComparer.Ordinal);
            return ValidationProblem(new ValidationProblemDetails(errors)
            {
                Title = "Password change rejected.",
            });
        }

        AuthenticationTokenResult authentication = result.Authentication
            ?? throw new InvalidOperationException("A successful password change did not issue replacement authentication tokens.");
        SetRefreshTokenCookie(authentication.RefreshToken, authentication.RefreshTokenExpiresAt);
        return Ok(new AuthTokenResponse(authentication.AccessToken.Value, authentication.AccessToken.ExpiresAt));
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
