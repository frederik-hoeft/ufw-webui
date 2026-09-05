using System.Net;
using System.Net.Http.Headers;

namespace Ufw.Client.Auth;

internal sealed class BearerTokenHandler(IAuthenticationService authenticationService) : DelegatingHandler
{
    protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? accessToken = await authenticationService.GetAccessTokenAsync(cancellationToken);
        HttpRequestReplaySnapshot? replay = accessToken is null
            ? null
            : await HttpRequestReplaySnapshot.CaptureAsync(request, cancellationToken);

        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        if (accessToken is null || response.StatusCode != HttpStatusCode.Unauthorized || replay is null)
        {
            return response;
        }

        string? replacementToken;
        try
        {
            replacementToken = await authenticationService.RefreshAfterUnauthorizedAsync(accessToken, cancellationToken);
        }
        catch
        {
            response.Dispose();
            throw;
        }

        if (replacementToken is null)
        {
            return response;
        }

        response.Dispose();
        using HttpRequestMessage retry = replay.CreateRequest();
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", replacementToken);

        HttpResponseMessage retryResponse = await base.SendAsync(retry, cancellationToken);
        if (retryResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            authenticationService.InvalidateAccessToken(replacementToken);
        }

        return retryResponse;
    }
}
