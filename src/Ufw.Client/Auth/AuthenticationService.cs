using Ufw.Client.Api;

namespace Ufw.Client.Auth;

internal sealed class AuthenticationService
(
    IAuthApiClient authApiClient,
    IAuthenticationSession session,
    IAuthenticationOperationCoordinator operationCoordinator,
    TimeProvider timeProvider
) : IAuthenticationService
{
    private static readonly TimeSpan s_refreshLeadTime = TimeSpan.FromSeconds(30);

    public Task InitializeAsync(CancellationToken cancellationToken = default) => RefreshAsync(rejectedAccessToken: null, cancellationToken);

    public Task LoginAsync(string email, string password, CancellationToken cancellationToken = default) => operationCoordinator.RunExclusiveAsync(
        async operationCancellationToken =>
        {
            AuthTokenResponse token = await authApiClient.LoginAsync(new LoginRequest(email, password), operationCancellationToken);
            session.SetToken(token.AccessToken, token.ExpiresAt);
        },
        cancellationToken);

    public Task LogoutAsync(CancellationToken cancellationToken = default) => operationCoordinator.RunExclusiveAsync(
        async operationCancellationToken =>
        {
            await authApiClient.LogoutAsync(operationCancellationToken);
            session.Clear();
        },
        cancellationToken);

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        (string AccessToken, DateTimeOffset ExpiresAt)? token = session.Token;
        if (token is { } current && IsFresh(current, s_refreshLeadTime))
        {
            return current.AccessToken;
        }

        return await RefreshAsync(rejectedAccessToken: null, cancellationToken);
    }

    public Task<string?> RefreshAfterUnauthorizedAsync(string rejectedAccessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rejectedAccessToken);
        return RefreshAsync(rejectedAccessToken, cancellationToken);
    }

    public void InvalidateAccessToken(string rejectedAccessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rejectedAccessToken);
        session.ClearIfCurrent(rejectedAccessToken);
    }

    private Task<string?> RefreshAsync(string? rejectedAccessToken, CancellationToken cancellationToken) => operationCoordinator.RunExclusiveAsync(
        async operationCancellationToken =>
        {
            (string AccessToken, DateTimeOffset ExpiresAt)? current = session.Token;
            if (rejectedAccessToken is null)
            {
                if (current is { } currentToken && IsFresh(currentToken, s_refreshLeadTime))
                {
                    return currentToken.AccessToken;
                }
            }
            else if (current is { } currentToken
                && !string.Equals(currentToken.AccessToken, rejectedAccessToken, StringComparison.Ordinal)
                && IsFresh(currentToken, TimeSpan.Zero))
            {
                return currentToken.AccessToken;
            }

            AuthTokenResponse? token = await authApiClient.TryRefreshAsync(operationCancellationToken);
            if (token is null)
            {
                session.Clear();
                return null;
            }

            session.SetToken(token.AccessToken, token.ExpiresAt);
            return token.AccessToken;
        },
        cancellationToken);

    private bool IsFresh((string AccessToken, DateTimeOffset ExpiresAt) token, TimeSpan requiredRemainingLifetime) =>
        token.ExpiresAt > timeProvider.GetUtcNow().Add(requiredRemainingLifetime);
}
