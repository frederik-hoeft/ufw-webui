using Microsoft.AspNetCore.Identity;

namespace Ufw.Web.Services.Auth;

internal interface IJwtTokenService
{
    Task<AccessToken> IssueAsync(IdentityUser user, CancellationToken cancellationToken = default);
}
