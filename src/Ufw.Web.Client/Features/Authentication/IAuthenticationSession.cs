namespace Ufw.Web.Client.Features.Authentication;

public interface IAuthenticationSession
{
    (string AccessToken, DateTimeOffset ExpiresAt)? Token { get; }

    void SetToken(string accessToken, DateTimeOffset expiresAt);

    void Clear();

    bool ClearIfCurrent(string accessToken);
}
