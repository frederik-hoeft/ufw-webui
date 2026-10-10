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

namespace Ufw.Web.Client.Api;

internal static class ClientApiServiceCollectionExtensions
{
    public static IServiceCollection AddClientApiServices(this IServiceCollection services)
    {
        // Liveness is intentionally anonymous and must not trigger refresh or cookie handling.
        services.AddApiClient<IManagementApiHealthClient, ManagementApiHealthClient>();
        services.AddApiClient<IAuthApiClient, AuthApiClient>()
            .AddHttpMessageHandler<BrowserCredentialsHandler>();

        services.AddAuthenticatedApiClient<IDaemonStatusApiClient, DaemonStatusApiClient>();
        services.AddAuthenticatedApiClient<IIntentContextApiClient, IntentContextApiClient>();
        services.AddAuthenticatedApiClient<IRuleApiClient, RuleApiClient>();
        services.AddAuthenticatedApiClient<IRuleMetadataReconciliationApiClient, RuleMetadataReconciliationApiClient>();
        services.AddAuthenticatedApiClient<IRuleTagApiClient, RuleTagApiClient>();
        services.AddAuthenticatedApiClient<IRuleGroupApiClient, RuleGroupApiClient>();
        services.AddAuthenticatedApiClient<IRuleTemplateApiClient, RuleTemplateApiClient>();
        services.AddAuthenticatedApiClient<IKnownHostApiClient, KnownHostApiClient>();
        services.AddAuthenticatedApiClient<INetworkInterfaceApiClient, NetworkInterfaceApiClient>();
        return services;
    }

    private static IHttpClientBuilder AddAuthenticatedApiClient<TClient, TImplementation>(this IServiceCollection services)
        where TClient : class
        where TImplementation : class, TClient => services.AddApiClient<TClient, TImplementation>()
            // Bearer must be outermost: 401 replay then re-enters the browser credentials handler.
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddHttpMessageHandler<BrowserCredentialsHandler>();

    private static IHttpClientBuilder AddApiClient<TClient, TImplementation>(this IServiceCollection services)
        where TClient : class
        where TImplementation : class, TClient => services.AddHttpClient<TClient, TImplementation>(
            static (provider, client) => client.BaseAddress = provider.GetRequiredService<ClientRuntimeConfiguration>().ApiBaseAddress);
}
