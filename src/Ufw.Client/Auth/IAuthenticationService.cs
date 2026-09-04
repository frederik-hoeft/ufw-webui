namespace Ufw.Client.Auth;

public interface IAuthenticationService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);

    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
