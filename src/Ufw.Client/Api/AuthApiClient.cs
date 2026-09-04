using System.Net;
using System.Net.Http.Json;
using Ufw.Client.Serialization;

namespace Ufw.Client.Api;

internal sealed class AuthApiClient(HttpClient httpClient) : IAuthApiClient
{
    public async Task<AuthTokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            "api/v1/auth/login",
            request,
            ClientJsonSerializerContext.Default.LoginRequest,
            cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.AuthTokenResponse, cancellationToken);
    }

    public async Task<AuthTokenResponse?> TryRefreshAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsync("api/v1/auth/refresh", content: null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.AuthTokenResponse, cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsync("api/v1/auth/logout", content: null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await response.CreateExceptionAsync(cancellationToken);
        }
    }
}
