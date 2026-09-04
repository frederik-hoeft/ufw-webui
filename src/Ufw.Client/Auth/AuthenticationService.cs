using Microsoft.Extensions.Logging;
using Ufw.Client.Api;

namespace Ufw.Client.Auth;

internal sealed class AuthenticationService(
    IAuthApiClient authApiClient,
    IAuthenticationSession session,
    ILogger<AuthenticationService> logger) : IAuthenticationService
{
    private static readonly TimeSpan s_refreshLeadTime = TimeSpan.FromSeconds(30);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await RefreshAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is ApiRequestException or HttpRequestException or InvalidOperationException)
        {
            session.Clear();
            logger.LogWarning(exception, "Could not restore the browser authentication session.");
        }
    }

    public async Task LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        AuthTokenResponse token = await authApiClient.LoginAsync(new LoginRequest(email, password), cancellationToken);
        session.SetToken(token.AccessToken, token.ExpiresAt);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await authApiClient.LogoutAsync(cancellationToken);
        }
        finally
        {
            session.Clear();
        }
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (session.AccessToken is not null
            && session.ExpiresAt is DateTimeOffset expiresAt
            && expiresAt > DateTimeOffset.UtcNow.Add(s_refreshLeadTime))
        {
            return session.AccessToken;
        }

        return await RefreshAsync(cancellationToken);
    }

    private async Task<string?> RefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (session.AccessToken is not null
                && session.ExpiresAt is DateTimeOffset expiresAt
                && expiresAt > DateTimeOffset.UtcNow.Add(s_refreshLeadTime))
            {
                return session.AccessToken;
            }

            AuthTokenResponse? token = await authApiClient.TryRefreshAsync(cancellationToken);
            if (token is null)
            {
                session.Clear();
                return null;
            }

            session.SetToken(token.AccessToken, token.ExpiresAt);
            return token.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
