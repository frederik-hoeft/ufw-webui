using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Moq;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Api.Auth;
using Ufw.Web.Client.Api.Intent;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Client.Api.NetworkInterfaces;
using Ufw.Web.Client.Api.RuleGroups;
using Ufw.Web.Client.Api.RuleMetadata;
using Ufw.Web.Client.Api.Rules;
using Ufw.Web.Client.Api.RuleTags;
using Ufw.Web.Client.Api.RuleTemplates;
using Ufw.Web.Client.Api.Status;
using Ufw.Web.Client.Configuration;
using Ufw.Web.Client.Features.Authentication;

namespace Ufw.Web.Client.Tests.Api;

[TestClass]
public sealed class ClientApiServiceCollectionExtensionsTests
{
    [TestMethod]
    public async Task RegisteredClients_ApplyTheirDistinctBaseAddressAndAuthenticationPoliciesAsync()
    {
        ConcurrentQueue<CapturedRequest> requests = new();
        Mock<IAuthenticationService> authentication = new();
        authentication.Setup(service => service.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("test-access-token");

        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiBaseUrl"] = "https://api.example.invalid/management/",
        }).Build();
        ServiceCollection services = new();
        services.AddSingleton(new ClientRuntimeConfiguration(configuration, new Uri("https://app.example.invalid/")));
        services.AddSingleton(authentication.Object);
        services.AddSingleton(Mock.Of<IAuthenticationNavigation>());
        services.AddTransient<BearerTokenHandler>();
        services.AddTransient<BrowserCredentialsHandler>();
        services.AddClientApiServices();
        services.ConfigureAll<HttpClientFactoryOptions>(options => options.HttpMessageHandlerBuilderActions.Add(
            builder => builder.PrimaryHandler = new CaptureHandler(requests)));

        using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
        Type[] authenticatedClients =
        [
            typeof(IDaemonStatusApiClient),
            typeof(IIntentContextApiClient),
            typeof(IRuleApiClient),
            typeof(IRuleMetadataReconciliationApiClient),
            typeof(IRuleTagApiClient),
            typeof(IRuleGroupApiClient),
            typeof(IRuleTemplateApiClient),
            typeof(IKnownHostApiClient),
            typeof(INetworkInterfaceApiClient),
        ];

        Assert.IsNotNull(provider.GetService<IManagementApiHealthClient>());
        Assert.IsNotNull(provider.GetService<IAuthApiClient>());
        await SendAsync(nameof(IManagementApiHealthClient));
        await SendAsync(nameof(IAuthApiClient));
        foreach (Type clientType in authenticatedClients)
        {
            Assert.IsNotNull(provider.GetService(clientType));
            await SendAsync(clientType.Name);
        }

        CapturedRequest[] captured = [.. requests];
        Assert.HasCount(authenticatedClients.Length + 2, captured);
        foreach (CapturedRequest request in captured)
        {
            Assert.AreEqual("https://api.example.invalid/management/check", request.Uri);
            Assert.AreEqual(request.ClientName is nameof(IManagementApiHealthClient) or nameof(IAuthApiClient) ? null : "test-access-token", request.Bearer);
            Assert.AreEqual(request.ClientName != nameof(IManagementApiHealthClient), request.IncludesCredentials);
        }

        async Task SendAsync(string name)
        {
            using HttpClient client = factory.CreateClient(name);
            using HttpRequestMessage message = new(HttpMethod.Get, "check");
            message.Headers.Add("X-Test-Client", name);
            using HttpResponseMessage response = await client.SendAsync(message);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private sealed record CapturedRequest(string ClientName, string Uri, string? Bearer, bool IncludesCredentials);

    private sealed class CaptureHandler(ConcurrentQueue<CapturedRequest> requests) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            bool includesCredentials = request.Options.TryGetValue(new HttpRequestOptionsKey<object>("WebAssemblyFetchOptions"), out object? fetchOptions)
                && fetchOptions is IReadOnlyDictionary<string, object> options
                && options.TryGetValue("credentials", out object? credentials)
                && Equals(credentials, "include");
            requests.Enqueue(new CapturedRequest(request.Headers.GetValues("X-Test-Client").Single(), request.RequestUri!.AbsoluteUri, request.Headers.Authorization?.Parameter, includesCredentials));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request });
        }
    }
}
