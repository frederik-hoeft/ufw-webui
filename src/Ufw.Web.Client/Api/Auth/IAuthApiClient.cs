using Ufw.Web.Client.Api.Auth.Model;
namespace Ufw.Web.Client.Api.Auth;

public interface IAuthApiClient
{
    Task<AuthTokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<AuthTokenResponse?> TryRefreshAsync(CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);
}
