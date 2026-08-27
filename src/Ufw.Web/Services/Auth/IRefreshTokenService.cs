using Microsoft.AspNetCore.Identity;

namespace Ufw.Web.Services.Auth;

public interface IRefreshTokenService
{
    Task<RefreshTokenIssueResult> IssueAsync(IdentityUser user, CancellationToken cancellationToken = default);

    Task<RefreshTokenRotationResult?> RotateAsync(string token, CancellationToken cancellationToken = default);

    Task RevokeFamilyAsync(string token, CancellationToken cancellationToken = default);
}
