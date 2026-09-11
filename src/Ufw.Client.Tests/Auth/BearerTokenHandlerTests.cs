using Moq;
using System.Net;
using System.Text;
using Ufw.Client.Auth;
using Ufw.Client.Tests.Support;

namespace Ufw.Client.Tests.Auth;

[TestClass]
public sealed class BearerTokenHandlerTests
{
    [TestMethod]
    public async Task SendAsync_AttachesCurrentAccessTokenAsync()
    {
        Mock<IAuthenticationService> authentication = new();
        authentication.Setup(service => service.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("token");
        RecordingHttpMessageHandler inner = new((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        using BearerTokenHandler handler = new(authentication.Object) { InnerHandler = inner };
        using HttpClient client = new(handler);

        using HttpResponseMessage response = await client.GetAsync("https://localhost/api/v1/rules");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("Bearer", inner.Requests[0].AuthorizationScheme);
        Assert.AreEqual("token", inner.Requests[0].AuthorizationParameter);
    }

    [TestMethod]
    public async Task SendAsync_UnauthorizedRefreshesOnceAndReplaysEquivalentRequestAsync()
    {
        Mock<IAuthenticationService> authentication = new();
        authentication.Setup(service => service.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("old");
        authentication.Setup(service => service.RefreshAfterUnauthorizedAsync("old", It.IsAny<CancellationToken>())).ReturnsAsync("new");
        RecordingHttpMessageHandler inner = new((_, call) => new HttpResponseMessage(call == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK));
        using BearerTokenHandler handler = new(authentication.Object) { InnerHandler = inner };
        using HttpClient client = new(handler);
        using HttpRequestMessage request = new(HttpMethod.Post, "https://localhost/api/v1/rules");
        request.Headers.Add("X-Test", "value");
        request.Content = new StringContent("payload", Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.HasCount(2, inner.Requests);
        Assert.AreEqual(inner.Requests[0].Method, inner.Requests[1].Method);
        Assert.AreEqual(inner.Requests[0].RequestUri, inner.Requests[1].RequestUri);
        CollectionAssert.AreEqual(inner.Requests[0].Content, inner.Requests[1].Content);
        CollectionAssert.AreEqual(inner.Requests[0].Headers["X-Test"], inner.Requests[1].Headers["X-Test"]);
        Assert.AreEqual("old", inner.Requests[0].AuthorizationParameter);
        Assert.AreEqual("new", inner.Requests[1].AuthorizationParameter);
        authentication.Verify(service => service.RefreshAfterUnauthorizedAsync("old", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task SendAsync_SecondUnauthorizedInvalidatesReplacementTokenWithoutThirdAttemptAsync()
    {
        Mock<IAuthenticationService> authentication = new();
        authentication.Setup(service => service.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("old");
        authentication.Setup(service => service.RefreshAfterUnauthorizedAsync("old", It.IsAny<CancellationToken>())).ReturnsAsync("new");
        RecordingHttpMessageHandler inner = new((_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using BearerTokenHandler handler = new(authentication.Object) { InnerHandler = inner };
        using HttpClient client = new(handler);

        using HttpResponseMessage response = await client.GetAsync("https://localhost/api/v1/rules");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.HasCount(2, inner.Requests);
        authentication.Verify(service => service.InvalidateAccessToken("new"), Times.Once);
    }

    [TestMethod]
    public async Task SendAsync_NoAccessTokenReturnsUnauthorizedWithoutRefreshAsync()
    {
        Mock<IAuthenticationService> authentication = new();
        authentication.Setup(service => service.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
        RecordingHttpMessageHandler inner = new((_, _) => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using BearerTokenHandler handler = new(authentication.Object) { InnerHandler = inner };
        using HttpClient client = new(handler);

        using HttpResponseMessage response = await client.GetAsync("https://localhost/api/v1/rules");

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.HasCount(1, inner.Requests);
        authentication.Verify(service => service.RefreshAfterUnauthorizedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
