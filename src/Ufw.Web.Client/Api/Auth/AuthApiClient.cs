using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ufw.Shared.Web;
using Ufw.Web.Client.Api;
using Ufw.Web.Model.V1.Auth;

namespace Ufw.Web.Client.Api.Auth;

internal sealed class AuthApiClient(HttpClient httpClient) : IAuthApiClient
{
    private static readonly Uri s_antiforgeryUri = new("api/v1/auth/antiforgery", UriKind.Relative);
    private static readonly Uri s_loginUri = new("api/v1/auth/login", UriKind.Relative);
    private static readonly Uri s_refreshUri = new("api/v1/auth/refresh", UriKind.Relative);
    private static readonly Uri s_passwordUri = new("api/v1/auth/password", UriKind.Relative);
    private static readonly Uri s_logoutUri = new("api/v1/auth/logout", UriKind.Relative);

    public async Task<AuthTokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(s_loginUri, request, ClientJsonSerializerContext.Default.LoginRequest, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.AuthTokenResponse, cancellationToken);
    }

    public async Task<AuthTokenResponse?> TryRefreshAsync(CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = await CreateAntiforgeryProtectedRequestAsync(HttpMethod.Post, s_refreshUri, cancellationToken);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.AuthTokenResponse, cancellationToken);
    }

    public async Task<AuthTokenResponse> ChangePasswordAsync(ChangePasswordRequest request, string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        using HttpRequestMessage message = new(HttpMethod.Post, s_passwordUri)
        {
            Content = JsonContent.Create(request, ClientJsonSerializerContext.Default.ChangePasswordRequest),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.AuthTokenResponse, cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = await CreateAntiforgeryProtectedRequestAsync(HttpMethod.Post, s_logoutUri, cancellationToken);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await response.CreateExceptionAsync(cancellationToken);
        }
    }

    private async Task<HttpRequestMessage> CreateAntiforgeryProtectedRequestAsync(HttpMethod method, Uri uri, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(s_antiforgeryUri, cancellationToken);
        AntiforgeryTokenResponse token = await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.AntiforgeryTokenResponse, cancellationToken);
        HttpRequestMessage request = new(method, uri);
        request.Headers.Add(BrowserRequestHeaders.CSRF_TOKEN, token.RequestToken);
        return request;
    }
}
