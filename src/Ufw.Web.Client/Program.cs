using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Api.Auth;
using Ufw.Web.Client.Api.Intent;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Client.Api.NetworkInterfaces;
using Ufw.Web.Client.Api.RuleMetadata;
using Ufw.Web.Client.Api.RuleTags;
using Ufw.Web.Client.Api.Rules;
using Ufw.Web.Client.Api.Status;
using Ufw.Web.Client.Configuration;
using Ufw.Web.Client.Features.Authentication;
using Ufw.Web.Client.Features.KnownHosts;
using Ufw.Web.Client.Features.NetworkInterfaces;
using Ufw.Web.Client.Features.Rules.Intent;
using Ufw.Web.Client.Features.Rules.Services;
using Ufw.Web.Client.Features.Status;
using Ufw.Web.Client.Services.Clipboard;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Client.Services.Localization;
using Ufw.Web.Client.Services.Storage;
using Ufw.Web.Client.Services.Theming;
using Ufw.Web.Client.UI;
using Ufw.Web.Client.UI.Components.Rules.Filtering;

namespace Ufw.Web.Client;

public static class Program
{
    public static async Task Main(string[] args)
    {
        WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        ClientRuntimeConfiguration runtimeConfiguration = new(builder.Configuration, new Uri(builder.HostEnvironment.BaseAddress, UriKind.Absolute));

        builder.Services.AddMudServices();
        builder.Services.AddScoped<ILocalStorage, BrowserLocalStorage>();
        builder.Services.AddScoped<IClipboardService, BrowserClipboardService>();
        builder.Services.AddClientLocalization(builder.Configuration);
        builder.Services.AddAuthorizationCore();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(runtimeConfiguration);
        builder.Services.AddRuleManagementServices();
        builder.Services.AddRuleFilterUiServices();

        builder.Services.AddSingleton<IAccessTokenPrincipalFactory, AccessTokenPrincipalFactory>();
        builder.Services.AddScoped<AuthenticationSession>();
        builder.Services.AddScoped<IAuthenticationSession>(static services => services.GetRequiredService<AuthenticationSession>());
        builder.Services.AddScoped<AuthenticationStateProvider>(static services => services.GetRequiredService<AuthenticationSession>());
        builder.Services.AddScoped<IAuthenticationOperationCoordinator, BrowserAuthenticationOperationCoordinator>();
        builder.Services.AddScoped<BrowserCredentialsHandler>();
        builder.Services.AddScoped<BearerTokenHandler>();
        builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
        builder.Services.AddScoped<IClientErrorMapper, ClientErrorMapper>();
        builder.Services.AddScoped<IBrowserIntentCryptoService, BrowserIntentCryptoService>();
        builder.Services.AddScoped<IIntentSigningService, BrowserIntentSigningService>();
        builder.Services.AddScoped<IClientThemeService, BrowserClientThemeService>();
        builder.Services.AddScoped<IKnownHostInventoryService, KnownHostInventoryService>();
        builder.Services.AddSingleton<IKnownHostSuggestionService, KnownHostSuggestionService>();
        builder.Services.AddScoped<INetworkInterfaceInventoryService, NetworkInterfaceInventoryService>();
        builder.Services.AddScoped<IOperationalStatusService, OperationalStatusService>();

        builder.Services.AddHttpClient<IManagementApiHealthClient, ManagementApiHealthClient>(
            static (services, client) => client.BaseAddress = services.GetRequiredService<ClientRuntimeConfiguration>().ApiBaseAddress);
        builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>(
            static (services, client) => client.BaseAddress = services.GetRequiredService<ClientRuntimeConfiguration>().ApiBaseAddress)
            .AddHttpMessageHandler<BrowserCredentialsHandler>();
        builder.Services.AddHttpClient<IDaemonStatusApiClient, DaemonStatusApiClient>(
            static (services, client) => client.BaseAddress = services.GetRequiredService<ClientRuntimeConfiguration>().ApiBaseAddress)
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddHttpMessageHandler<BrowserCredentialsHandler>();
        // Keep browser credentials inside the bearer handler so a one-time 401 replay reapplies cookie credentials.
        builder.Services.AddHttpClient<IIntentContextApiClient, IntentContextApiClient>(
            static (services, client) => client.BaseAddress = services.GetRequiredService<ClientRuntimeConfiguration>().ApiBaseAddress)
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddHttpMessageHandler<BrowserCredentialsHandler>();
        builder.Services.AddHttpClient<IRuleApiClient, RuleApiClient>(
            static (services, client) => client.BaseAddress = services.GetRequiredService<ClientRuntimeConfiguration>().ApiBaseAddress)
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddHttpMessageHandler<BrowserCredentialsHandler>();
        builder.Services.AddHttpClient<IRuleMetadataReconciliationApiClient, RuleMetadataReconciliationApiClient>(
            static (services, client) => client.BaseAddress = services.GetRequiredService<ClientRuntimeConfiguration>().ApiBaseAddress)
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddHttpMessageHandler<BrowserCredentialsHandler>();
        builder.Services.AddHttpClient<IRuleTagApiClient, RuleTagApiClient>(
            static (services, client) => client.BaseAddress = services.GetRequiredService<ClientRuntimeConfiguration>().ApiBaseAddress)
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddHttpMessageHandler<BrowserCredentialsHandler>();
        builder.Services.AddHttpClient<IKnownHostApiClient, KnownHostApiClient>(
            static (services, client) => client.BaseAddress = services.GetRequiredService<ClientRuntimeConfiguration>().ApiBaseAddress)
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddHttpMessageHandler<BrowserCredentialsHandler>();
        builder.Services.AddHttpClient<INetworkInterfaceApiClient, NetworkInterfaceApiClient>(
            static (services, client) => client.BaseAddress = services.GetRequiredService<ClientRuntimeConfiguration>().ApiBaseAddress)
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddHttpMessageHandler<BrowserCredentialsHandler>();

        await builder.Build().RunAsync();
    }
}
