using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ufw.Web.Api.V1.Models.Auth;
using Ufw.Web.Configuration;
using Ufw.Web.Data;
using Ufw.Web.Services.Auth;
using Wkg.AspNetCore.Abstractions.Controllers;
using Wkg.AspNetCore.Transactions;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class AuthController
(
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    IAuthenticationTimingService authenticationTimingService,
    IOptions<RefreshTokenOptions> refreshTokenOptions,
    ITransactionServiceHandle transactionService
) : DatabaseController<ApplicationDbContext>(transactionService)
{
    private readonly RefreshTokenOptions _refreshTokenOptions = refreshTokenOptions.Value;

    public partial Task<IActionResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken) =>
        Transaction.Scoped.RunAsync<IActionResult>(async (_, transaction, ct) =>
        {
            ArgumentNullException.ThrowIfNull(request);

            IdentityUser? user = await userManager.FindByEmailAsync(request.Email);
            if (user is null)
            {
                authenticationTimingService.PerformDummyPasswordVerification(request.Password);
                return transaction.Rollback(Unauthorized());
            }

            SignInResult result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                if (result.IsLockedOut || result.IsNotAllowed)
                {
                    authenticationTimingService.PerformDummyPasswordVerification(request.Password);
                }

                // Identity may update the failed-access count or lockout state even though
                // authentication failed, so this is an expected write and must be committed.
                return transaction.Commit(Unauthorized());
            }

            AccessToken accessToken = await jwtTokenService.IssueAsync(user, ct);
            RefreshTokenIssueResult refreshToken = await refreshTokenService.IssueAsync(user, ct);
            SetRefreshTokenCookie(refreshToken.Token, refreshToken.ExpiresAt);

            return transaction.Commit(Ok(new AuthTokenResponse(accessToken.Value, accessToken.ExpiresAt)));
        }, cancellationToken);

    public partial Task<IActionResult> RefreshAsync(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(_refreshTokenOptions.CookieName, out string? refreshToken)
            || string.IsNullOrWhiteSpace(refreshToken))
        {
            return Task.FromResult<IActionResult>(Unauthorized());
        }

        return Transaction.Scoped.RunAsync<IActionResult>(async (_, transaction, ct) =>
        {
            RefreshTokenRotationResult? rotation = await refreshTokenService.RotateAsync(refreshToken, ct);
            if (rotation is null)
            {
                // Invalid/replayed tokens can revoke persistent family state. Committing is
                // therefore required even though the client receives an unauthorized result.
                DeleteRefreshTokenCookie();
                return transaction.Commit(Unauthorized());
            }

            bool canSignIn = await signInManager.CanSignInAsync(rotation.User);
            bool isLockedOut = userManager.SupportsUserLockout && await userManager.IsLockedOutAsync(rotation.User);
            if (!canSignIn || isLockedOut)
            {
                await refreshTokenService.RevokeFamilyAsync(rotation.Token, ct);
                DeleteRefreshTokenCookie();
                return transaction.Commit(Unauthorized());
            }

            AccessToken accessToken = await jwtTokenService.IssueAsync(rotation.User, ct);
            SetRefreshTokenCookie(rotation.Token, rotation.ExpiresAt);
            return transaction.Commit(Ok(new AuthTokenResponse(accessToken.Value, accessToken.ExpiresAt)));
        }, cancellationToken);
    }

    public partial Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(_refreshTokenOptions.CookieName, out string? refreshToken)
            || string.IsNullOrWhiteSpace(refreshToken))
        {
            DeleteRefreshTokenCookie();
            return Task.FromResult<IActionResult>(NoContent());
        }

        return Transaction.Scoped.RunAsync<IActionResult>(async (_, transaction, ct) =>
        {
            await refreshTokenService.RevokeFamilyAsync(refreshToken, ct);
            DeleteRefreshTokenCookie();
            return transaction.Commit(NoContent());
        }, cancellationToken);
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
