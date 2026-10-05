using Microsoft.AspNetCore.Identity;
using Ufw.Web.Data;
using Ufw.Web.Data.Access.Auth;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Ufw.Web.Services.Auth;

internal sealed class AuthenticationFlowService
(
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    IJwtTokenService jwtTokenService,
    IRefreshTokenDataAccess refreshTokens,
    IAuthenticationTimingService authenticationTimingService,
    ITransactionServiceHandle transactionService
) : DatabaseService<ApplicationDbContext>(transactionService), IAuthenticationFlowService
{
    public Task<AuthenticationTokenResult?> LoginAsync(string email, string password, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<AuthenticationTokenResult?>(async (context, transaction, ct) =>
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
            RefreshTokenIssueResult refreshToken = await refreshTokens.IssueAsync(context, user.Id, user.SecurityStamp, ct);
            return transaction.Commit<AuthenticationTokenResult?>(new AuthenticationTokenResult(accessToken, refreshToken.Token, refreshToken.ExpiresAt));
        }, cancellationToken);

    public Task<AuthenticationTokenResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<AuthenticationTokenResult?>(async (context, transaction, ct) =>
        {
            RefreshTokenRotationResult? rotation = await refreshTokens.RotateAsync(context, refreshToken, ct);
            if (rotation is null)
            {
                // Invalid/replayed tokens can revoke persistent family state. Committing is
                // therefore required even though the client receives an unauthorized result.
                return transaction.Commit<AuthenticationTokenResult?>(null);
            }

            IdentityUser? user = await userManager.FindByIdAsync(rotation.UserId);
            if (user is null)
            {
                await refreshTokens.RevokeFamilyAsync(context, rotation.Token, ct);
                return transaction.Commit<AuthenticationTokenResult?>(null);
            }

            bool canSignIn = await signInManager.CanSignInAsync(user);
            bool isLockedOut = userManager.SupportsUserLockout && await userManager.IsLockedOutAsync(user);
            if (!canSignIn || isLockedOut)
            {
                await refreshTokens.RevokeFamilyAsync(context, rotation.Token, ct);
                return transaction.Commit<AuthenticationTokenResult?>(null);
            }

            AccessToken accessToken = await jwtTokenService.IssueAsync(user, ct);
            return transaction.Commit<AuthenticationTokenResult?>(new AuthenticationTokenResult(accessToken, rotation.Token, rotation.ExpiresAt));
        }, cancellationToken);

    public Task<PasswordChangeResult?> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<PasswordChangeResult?>(async (context, transaction, ct) =>
        {
            IdentityUser? user = await userManager.FindByIdAsync(userId);
            if (user is null)
            {
                return transaction.Rollback<PasswordChangeResult?>(null);
            }

            IdentityResult changeResult = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (!changeResult.Succeeded)
            {
                IReadOnlyList<PasswordChangeValidationError> errors = IdentityPasswordChangeErrorMapper.Map(changeResult.Errors);
                return transaction.Rollback<PasswordChangeResult?>(new PasswordChangeResult(errors, Authentication: null));
            }

            await refreshTokens.RevokeUserAsync(context, user.Id, ct);
            AccessToken accessToken = await jwtTokenService.IssueAsync(user, ct);
            RefreshTokenIssueResult refreshToken = await refreshTokens.IssueAsync(context, user.Id, user.SecurityStamp, ct);
            AuthenticationTokenResult authentication = new(accessToken, refreshToken.Token, refreshToken.ExpiresAt);
            return transaction.Commit<PasswordChangeResult?>(new PasswordChangeResult([], authentication));
        }, cancellationToken);

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        _ = await Transaction.Scoped.RunAsync(async (context, transaction, ct) =>
        {
            await refreshTokens.RevokeFamilyAsync(context, refreshToken, ct);
            return transaction.Commit(true);
        }, cancellationToken);
    }
}
