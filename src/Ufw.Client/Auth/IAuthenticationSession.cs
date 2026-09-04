namespace Ufw.Client.Auth;

public interface IAuthenticationSession
{
    string? AccessToken { get; }

    DateTimeOffset? ExpiresAt { get; }

    void SetToken(string accessToken, DateTimeOffset expiresAt);

    void Clear();
}
