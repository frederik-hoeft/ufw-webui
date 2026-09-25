namespace Ufw.Web.Client.Features.Authentication;

public interface IAuthenticationService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);

    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    Task<string?> RefreshAfterUnauthorizedAsync(string rejectedAccessToken, CancellationToken cancellationToken = default);

    void InvalidateAccessToken(string rejectedAccessToken);
}
