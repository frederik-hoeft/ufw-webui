using System.Net;
using System.Net.Http.Headers;

namespace Ufw.Client.Auth;

internal sealed class BearerTokenHandler(
    IAuthenticationService authenticationService,
    IAuthenticationSession session) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? accessToken = await authenticationService.GetAccessTokenAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        if (accessToken is not null && response.StatusCode == HttpStatusCode.Unauthorized)
        {
            session.Clear();
        }

        return response;
    }
}
