using System.Security.Claims;

namespace Ufw.Web.Client.Features.Authentication;

internal interface IAccessTokenPrincipalFactory
{
    ClaimsPrincipal CreatePrincipal(string accessToken);
}
