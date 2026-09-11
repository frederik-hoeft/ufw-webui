namespace Ufw.Web.Services.Auth;

public interface IAuthenticationFlowService
{
    Task<AuthenticationTokenResult?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<AuthenticationTokenResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);
}
