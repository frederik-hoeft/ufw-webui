using System.Net;
using System.Net.Http.Headers;

namespace Ufw.Web.Client.Features.Authentication;

internal sealed class BearerTokenHandler(IAuthenticationService authenticationService, IAuthenticationNavigation authenticationNavigation) : DelegatingHandler
{
    protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? accessToken = await authenticationService.GetAccessTokenAsync(cancellationToken);
        HttpRequestReplaySnapshot? replay = null;
        if (accessToken is not null)
        {
            replay = await HttpRequestReplaySnapshot.CaptureAsync(request, cancellationToken);
        }

        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }
        if (accessToken is null || replay is null)
        {
            authenticationNavigation.RedirectToLogin();
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
            authenticationNavigation.RedirectToLogin();
            return response;
        }

        response.Dispose();
        using HttpRequestMessage retry = replay.CreateRequest();
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", replacementToken);

        HttpResponseMessage retryResponse = await base.SendAsync(retry, cancellationToken);
        if (retryResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            authenticationService.InvalidateAccessToken(replacementToken);
            authenticationNavigation.RedirectToLogin();
        }

        return retryResponse;
    }
}
