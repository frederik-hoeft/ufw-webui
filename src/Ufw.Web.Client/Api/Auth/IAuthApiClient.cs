using Ufw.Web.Model.V1.Auth;

namespace Ufw.Web.Client.Api.Auth;

public interface IAuthApiClient
{
    Task<AuthTokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<AuthTokenResponse?> TryRefreshAsync(CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);
}
