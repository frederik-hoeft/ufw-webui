using Microsoft.AspNetCore.Components.WebAssembly.Http;
using System.Net;
using Ufw.Client.Auth;
using Ufw.Client.Tests.Support;

namespace Ufw.Client.Tests.Auth;

[TestClass]
public sealed class BrowserCredentialsHandlerTests
{
    [TestMethod]
    public async Task SendAsync_MarksRequestToIncludeBrowserCredentialsAsync()
    {
        using RecordingHttpMessageHandler inner = new((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        using BrowserCredentialsHandler handler = new() { InnerHandler = inner };
        using HttpClient client = new(handler);

        using HttpResponseMessage response = await client.GetAsync("https://localhost/api/v1/auth/refresh");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.HasCount(1, inner.Requests);
        IReadOnlyDictionary<string, object> options = (IReadOnlyDictionary<string, object>)inner.Requests[0].Options["WebAssemblyFetchOptions"]!;
        Assert.AreEqual("include", options["credentials"]);
    }
}
