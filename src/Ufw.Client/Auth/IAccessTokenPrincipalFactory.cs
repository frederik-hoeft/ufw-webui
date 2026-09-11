using System.Security.Claims;

namespace Ufw.Client.Auth;

internal interface IAccessTokenPrincipalFactory
{
    ClaimsPrincipal CreatePrincipal(string accessToken);
}
