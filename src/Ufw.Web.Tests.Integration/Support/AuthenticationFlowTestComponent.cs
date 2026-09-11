using Ufw.Web.Services.Auth;

namespace Ufw.Web.Tests.Integration.Support;

public sealed class AuthenticationFlowTestComponent(IAuthenticationFlowService authenticationFlowService)
{
    public Task<AuthenticationTokenResult?> LoginAsync(string email, string password, CancellationToken cancellationToken = default) =>
        authenticationFlowService.LoginAsync(email, password, cancellationToken);

    public Task<AuthenticationTokenResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        authenticationFlowService.RefreshAsync(refreshToken, cancellationToken);

    public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        authenticationFlowService.RevokeAsync(refreshToken, cancellationToken);
}
