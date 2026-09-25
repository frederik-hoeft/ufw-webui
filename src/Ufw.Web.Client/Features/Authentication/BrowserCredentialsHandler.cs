using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Ufw.Shared.Web;

namespace Ufw.Web.Client.Features.Authentication;

internal sealed class BrowserCredentialsHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head && request.Method != HttpMethod.Options && request.Method != HttpMethod.Trace)
        {
            request.Headers.Remove(BrowserRequestHeaders.CSRF_PROTECTION);
            request.Headers.Add(BrowserRequestHeaders.CSRF_PROTECTION, BrowserRequestHeaders.CSRF_PROTECTION_VALUE);
        }
        return base.SendAsync(request, cancellationToken);
    }
}
