using System.Net;
using System.Net.Http.Json;
using Ufw.Client.Serialization;

namespace Ufw.Client.Api;

internal sealed class AuthApiClient(HttpClient httpClient) : IAuthApiClient
{
    private static readonly Uri s_loginUri = new("api/v1/auth/login", UriKind.Relative);
    private static readonly Uri s_refreshUri = new("api/v1/auth/refresh", UriKind.Relative);
    private static readonly Uri s_logoutUri = new("api/v1/auth/logout", UriKind.Relative);

    public async Task<AuthTokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            s_loginUri,
            request,
            ClientJsonSerializerContext.Default.LoginRequest,
            cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.AuthTokenResponse, cancellationToken);
    }

    public async Task<AuthTokenResponse?> TryRefreshAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsync(s_refreshUri, content: null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.AuthTokenResponse, cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsync(s_logoutUri, content: null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await response.CreateExceptionAsync(cancellationToken);
        }
    }
}
