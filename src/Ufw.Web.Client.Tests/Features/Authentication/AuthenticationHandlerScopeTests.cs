using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Ufw.Web.Client.Api.Auth;
using Ufw.Web.Client.Features.Authentication;

namespace Ufw.Web.Client.Tests.Features.Authentication;

[TestClass]
public sealed class AuthenticationHandlerScopeTests
{
    [TestMethod]
    public async Task TwoFactoryHandlerScopesUseOneApplicationSessionAsync()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Mock.Of<IJSRuntime>());
        services.AddSingleton(Mock.Of<IAuthApiClient>());
        services.AddSingleton<Microsoft.AspNetCore.Components.NavigationManager>(new Ufw.Web.Client.Tests.Support.TestNavigationManager());
        Mock<IAccessTokenPrincipalFactory> principalFactory = new();
        principalFactory.Setup(factory => factory.CreatePrincipal("test-access-token"))
            .Returns(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "administrator")], "jwt")));
        services.AddBrowserAuthentication();
        services.AddSingleton(principalFactory.Object);
        List<string?> observed = [];
        services.AddHttpClient("rules")
            .AddHttpMessageHandler<BearerTokenHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => new CaptureHandler(observed));
        services.AddHttpClient("templates")
            .AddHttpMessageHandler<BearerTokenHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => new CaptureHandler(observed));

        using ServiceProvider provider = services.BuildServiceProvider();
        IAuthenticationSession session = provider.GetRequiredService<IAuthenticationSession>();
        AuthenticationStateProvider state = provider.GetRequiredService<AuthenticationStateProvider>();
        Assert.AreSame((object)session, state);
        session.SetToken("test-access-token", TimeProvider.System.GetUtcNow().AddMinutes(2));

        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
        using HttpClient rules = factory.CreateClient("rules");
        using HttpClient templates = factory.CreateClient("templates");
        using HttpResponseMessage rulesResponse = await rules.GetAsync("https://localhost/rules");
        using HttpResponseMessage templatesResponse = await templates.GetAsync("https://localhost/templates");

        CollectionAssert.AreEqual(new[] { "test-access-token", "test-access-token" }, observed);
        Assert.IsTrue((await state.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
        session.Clear();
        Assert.IsFalse((await state.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
    }

    private sealed class CaptureHandler(ICollection<string?> seen) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            seen.Add(request.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request });
        }
    }
}
