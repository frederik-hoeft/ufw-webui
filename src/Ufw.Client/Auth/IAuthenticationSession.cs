namespace Ufw.Client.Auth;

public interface IAuthenticationSession
{
    (string AccessToken, DateTimeOffset ExpiresAt)? Token { get; }

    void SetToken(string accessToken, DateTimeOffset expiresAt);

    void Clear();

    bool ClearIfCurrent(string accessToken);
}
