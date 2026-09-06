using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Client.Api;
using Ufw.Client.Auth;
using Ufw.Client.Configuration;
using Ufw.Client.Components.Rules;
using Ufw.Client.Errors;
using Ufw.Client.Intent;
using Ufw.Client.Localization;
using Ufw.Client.NetworkInterfaces;
using Ufw.Client.RuleOrdering;
using Ufw.Client.Status;
using Ufw.Client.Storage;
using Ufw.Client.Theming;

namespace Ufw.Client;

public static class Program
{
    public static async Task Main(string[] args)
    {
        WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        Uri apiBaseAddress = ClientRuntimeConfiguration.GetApiBaseAddress(builder.Configuration);

        builder.Services.AddMudServices();
        builder.Services.AddScoped<ILocalStorage, BrowserLocalStorage>();
        builder.Services.AddClientLocalization(builder.Configuration);
        builder.Services.AddAuthorizationCore();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IUfwRuleCommandRenderer, UfwRuleCommandRenderer>();
        builder.Services.AddScoped<IFirewallRuleText, FirewallRuleText>();
        builder.Services.AddScoped<IRuleValidationMessageLocalizer, RuleValidationMessageLocalizer>();

        builder.Services.AddScoped<AuthenticationSession>();
        builder.Services.AddScoped<IAuthenticationSession>(static services => services.GetRequiredService<AuthenticationSession>());
        builder.Services.AddScoped<AuthenticationStateProvider>(static services => services.GetRequiredService<AuthenticationSession>());
        builder.Services.AddScoped<IAuthenticationOperationCoordinator, BrowserAuthenticationOperationCoordinator>();
        builder.Services.AddScoped<BrowserCredentialsHandler>();
        builder.Services.AddScoped<BearerTokenHandler>();
        builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
        builder.Services.AddScoped<IClientErrorMapper, ClientErrorMapper>();
        builder.Services.AddScoped<IIntentSigningService, BrowserIntentSigningService>();
        builder.Services.AddScoped<IClientThemeService, BrowserClientThemeService>();
        builder.Services.AddScoped<INetworkInterfaceApiClient, MockNetworkInterfaceApiClient>();
        builder.Services.AddScoped<INetworkInterfaceInventoryService, NetworkInterfaceInventoryService>();
        builder.Services.AddScoped<IRuleOrderingApiClient, MockRuleOrderingApiClient>();
        builder.Services.AddScoped<IRuleOrderingProjectionService, RuleOrderingProjectionService>();
        builder.Services.AddScoped<IOperationalStatusService, OperationalStatusService>();

        builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<BrowserCredentialsHandler>();
        // Keep browser credentials inside the bearer handler so a one-time 401 replay reapplies cookie credentials.
        builder.Services.AddHttpClient<IUfwApiClient, UfwApiClient>(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddHttpMessageHandler<BrowserCredentialsHandler>();

        await builder.Build().RunAsync();
    }
}
