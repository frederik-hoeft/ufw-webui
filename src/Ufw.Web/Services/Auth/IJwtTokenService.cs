using Microsoft.AspNetCore.Identity;

namespace Ufw.Web.Services.Auth;

public interface IJwtTokenService
{
    Task<AccessToken> IssueAsync(IdentityUser user, CancellationToken cancellationToken = default);
}
