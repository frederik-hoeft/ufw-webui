using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.Text.Json;
using Ufw.Client.Api;

namespace Ufw.Client.Auth;

internal sealed class AuthenticationSession(IAccessTokenPrincipalFactory principalFactory) : AuthenticationStateProvider, IAuthenticationSession
{
    private static readonly AuthenticationState s_anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private readonly Lock _sync = new();
    private AuthenticationState _state = s_anonymous;
    private (string AccessToken, DateTimeOffset ExpiresAt)? _token;

    public (string AccessToken, DateTimeOffset ExpiresAt)? Token
    {
        get
        {
            lock (_sync)
            {
                return _token;
            }
        }
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        lock (_sync)
        {
            return Task.FromResult(_state);
        }
    }

    public void SetToken(string accessToken, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        ClaimsPrincipal principal;
        try
        {
            principal = principalFactory.CreatePrincipal(accessToken);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException)
        {
            throw new ApiProtocolException("The management API returned an invalid access token.", exception);
        }

        AuthenticationState state = new(principal);
        lock (_sync)
        {
            _token = (accessToken, expiresAt);
            _state = state;
        }

        NotifyAuthenticationStateChanged(Task.FromResult(state));
    }

    public void Clear()
    {
        bool changed;
        lock (_sync)
        {
            changed = _token is not null || !ReferenceEquals(_state, s_anonymous);
            _token = null;
            _state = s_anonymous;
        }

        if (changed)
        {
            NotifyAuthenticationStateChanged(Task.FromResult(s_anonymous));
        }
    }

    public bool ClearIfCurrent(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        bool changed;
        lock (_sync)
        {
            changed = _token is { AccessToken: string current }
                && string.Equals(current, accessToken, StringComparison.Ordinal);
            if (changed)
            {
                _token = null;
                _state = s_anonymous;
            }
        }

        if (changed)
        {
            NotifyAuthenticationStateChanged(Task.FromResult(s_anonymous));
        }

        return changed;
    }
}
