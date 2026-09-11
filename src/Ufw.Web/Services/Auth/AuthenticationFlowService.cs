using Microsoft.AspNetCore.Identity;
using Ufw.Web.Data;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Ufw.Web.Services.Auth;

internal sealed class AuthenticationFlowService
(
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    IAuthenticationTimingService authenticationTimingService,
    ITransactionServiceHandle transactionService
) : DatabaseService<ApplicationDbContext>(transactionService), IAuthenticationFlowService
{
    public Task<AuthenticationTokenResult?> LoginAsync(string email, string password, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<AuthenticationTokenResult?>(async (_, transaction, ct) =>
        {
            IdentityUser? user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                authenticationTimingService.PerformDummyPasswordVerification(password);
                return transaction.Rollback<AuthenticationTokenResult?>(null);
            }

            SignInResult result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                if (result.IsLockedOut || result.IsNotAllowed)
                {
                    authenticationTimingService.PerformDummyPasswordVerification(password);
                }

                // Identity may update the failed-access count or lockout state even though
                // authentication failed, so this is an expected write and must be committed.
                return transaction.Commit<AuthenticationTokenResult?>(null);
            }

            AccessToken accessToken = await jwtTokenService.IssueAsync(user, ct);
            RefreshTokenIssueResult refreshToken = await refreshTokenService.IssueAsync(user, ct);
            return transaction.Commit<AuthenticationTokenResult?>(new AuthenticationTokenResult(accessToken, refreshToken.Token, refreshToken.ExpiresAt));
        }, cancellationToken);

    public Task<AuthenticationTokenResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<AuthenticationTokenResult?>(async (_, transaction, ct) =>
        {
            RefreshTokenRotationResult? rotation = await refreshTokenService.RotateAsync(refreshToken, ct);
            if (rotation is null)
            {
                // Invalid/replayed tokens can revoke persistent family state. Committing is
                // therefore required even though the client receives an unauthorized result.
                return transaction.Commit<AuthenticationTokenResult?>(null);
            }

            bool canSignIn = await signInManager.CanSignInAsync(rotation.User);
            bool isLockedOut = userManager.SupportsUserLockout && await userManager.IsLockedOutAsync(rotation.User);
            if (!canSignIn || isLockedOut)
            {
                await refreshTokenService.RevokeFamilyAsync(rotation.Token, ct);
                return transaction.Commit<AuthenticationTokenResult?>(null);
            }

            AccessToken accessToken = await jwtTokenService.IssueAsync(rotation.User, ct);
            return transaction.Commit<AuthenticationTokenResult?>(new AuthenticationTokenResult(accessToken, rotation.Token, rotation.ExpiresAt));
        }, cancellationToken);

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        _ = await Transaction.Scoped.RunAsync(async (_, transaction, ct) =>
        {
            await refreshTokenService.RevokeFamilyAsync(refreshToken, ct);
            return transaction.Commit(true);
        }, cancellationToken);
    }
}
