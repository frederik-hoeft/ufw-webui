namespace Ufw.Web.Services.Auth;

public interface IAuthenticationFlowService
{
    Task<AuthenticationTokenResult?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<AuthenticationTokenResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task<PasswordChangeResult?> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);
}
