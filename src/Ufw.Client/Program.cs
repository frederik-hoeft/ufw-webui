using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Ufw.Client.Api;
using Ufw.Client.Auth;
using Ufw.Client.Components.Rules;
using Ufw.Client.Components.Rules.Filtering;
using Ufw.Client.Components.Rules.Filtering.Actions;
using Ufw.Client.Components.Rules.Filtering.Directions;
using Ufw.Client.Components.Rules.Filtering.Networks;
using Ufw.Client.Components.Rules.Filtering.Ports;
using Ufw.Client.Components.Rules.Filtering.Protocols;
using Ufw.Client.Components.Rules.Filtering.Text;
using Ufw.Client.Configuration;
using Ufw.Client.Errors;
using Ufw.Client.Intent;
using Ufw.Client.KnownHosts;
using Ufw.Client.Localization;
using Ufw.Client.NetworkInterfaces;
using Ufw.Client.RuleInsertion;
using Ufw.Client.RuleOrdering;
using Ufw.Client.Rules;
using Ufw.Client.Rules.Authoring;
using Ufw.Client.Rules.Filtering;
using Ufw.Client.Rules.Filtering.Actions;
using Ufw.Client.Rules.Filtering.Directions;
using Ufw.Client.Rules.Filtering.Networks;
using Ufw.Client.Rules.Filtering.Ports;
using Ufw.Client.Rules.Filtering.Protocols;
using Ufw.Client.Rules.Filtering.Text;
using Ufw.Client.Rules.Presentation;
using Ufw.Client.Status;
using Ufw.Client.Storage;
using Ufw.Client.Theming;
using Ufw.Shared.Firewall.Rendering;

namespace Ufw.Client;

public static class Program
{
    public static async Task Main(string[] args)
    {
        WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        Uri apiBaseAddress = ClientRuntimeConfiguration.GetApiBaseAddress(builder.Configuration, new Uri(builder.HostEnvironment.BaseAddress, UriKind.Absolute));

        builder.Services.AddMudServices();
        builder.Services.AddScoped<ILocalStorage, BrowserLocalStorage>();
        builder.Services.AddClientLocalization(builder.Configuration);
        builder.Services.AddAuthorizationCore();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IUfwRuleCommandRenderer, UfwRuleCommandRenderer>();
        builder.Services.AddScoped<IFirewallRuleText, FirewallRuleText>();
        builder.Services.AddScoped<IRuleValidationMessageLocalizer, RuleValidationMessageLocalizer>();
        builder.Services.AddScoped<IRuleEditorValidationService, RuleEditorValidationService>();
        builder.Services.AddScoped<IRuleEditorReferenceDataService, RuleEditorReferenceDataService>();
        builder.Services.AddSingleton<IRuleDraftFactory, RuleDraftFactory>();

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
        builder.Services.AddScoped<IRuleMutationService, RuleMutationService>();
        builder.Services.AddScoped<IClientThemeService, BrowserClientThemeService>();
        builder.Services.AddScoped<IKnownHostInventoryService, KnownHostInventoryService>();
        builder.Services.AddSingleton<IKnownHostSuggestionService, KnownHostSuggestionService>();
        builder.Services.AddScoped<INetworkInterfaceInventoryService, NetworkInterfaceInventoryService>();
        builder.Services.AddScoped<IRuleOrderingService, RuleOrderingService>();
        builder.Services.AddScoped<IRuleOrderingProjectionService, RuleOrderingProjectionService>();
        builder.Services.AddSingleton<IRuleOrderingResultProjectionService, RuleOrderingResultProjectionService>();
        builder.Services.AddSingleton<IRuleListProjectionService, RuleListProjectionService>();
        builder.Services.AddSingleton<IRuleFilterDefinitionProvider, NetworkRuleFilterDefinitionProvider>();
        builder.Services.AddSingleton<IRuleFilterDefinitionProvider, PortRuleFilterDefinitionProvider>();
        builder.Services.AddSingleton<IRuleFilterDefinitionProvider, ProtocolRuleFilterDefinitionProvider>();
        builder.Services.AddSingleton<IRuleFilterDefinitionProvider, ActionRuleFilterDefinitionProvider>();
        builder.Services.AddSingleton<IRuleFilterDefinitionProvider, DirectionRuleFilterDefinitionProvider>();
        builder.Services.AddSingleton<IRuleFilterDefinitionProvider, TextRuleFilterDefinitionProvider>();
        builder.Services.AddSingleton<IRuleFilterCatalog, RuleFilterCatalog>();
        builder.Services.AddSingleton<IRuleFilterEvaluator, NetworkRuleFilterEvaluator>();
        builder.Services.AddSingleton<IRuleFilterEvaluator, PortRuleFilterEvaluator>();
        builder.Services.AddSingleton<IRuleFilterEvaluator, ProtocolRuleFilterEvaluator>();
        builder.Services.AddSingleton<IRuleFilterEvaluator, ActionRuleFilterEvaluator>();
        builder.Services.AddSingleton<IRuleFilterEvaluator, DirectionRuleFilterEvaluator>();
        builder.Services.AddSingleton<IRuleFilterEvaluator, TextRuleFilterEvaluator>();
        builder.Services.AddSingleton<IRuleQueryService, RuleQueryService>();
        builder.Services.AddSingleton<IRuleInsertionNavigationService, RuleInsertionNavigationService>();
        builder.Services.AddSingleton<IRuleMutationReconciliationService, RuleMutationReconciliationService>();
        builder.Services.AddScoped<IOperationalStatusService, OperationalStatusService>();

        builder.Services.AddHttpClient<IManagementApiHealthClient, ManagementApiHealthClient>(client => client.BaseAddress = apiBaseAddress);
        builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<BrowserCredentialsHandler>();
        builder.Services.AddHttpClient<IDaemonStatusApiClient, DaemonStatusApiClient>(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddHttpMessageHandler<BrowserCredentialsHandler>();
        // Keep browser credentials inside the bearer handler so a one-time 401 replay reapplies cookie credentials.
        builder.Services.AddHttpClient<IIntentContextApiClient, IntentContextApiClient>(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddHttpMessageHandler<BrowserCredentialsHandler>();
        builder.Services.AddHttpClient<IRuleApiClient, RuleApiClient>(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddHttpMessageHandler<BrowserCredentialsHandler>();
        builder.Services.AddHttpClient<IKnownHostApiClient, KnownHostApiClient>(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddHttpMessageHandler<BrowserCredentialsHandler>();
        builder.Services.AddHttpClient<INetworkInterfaceApiClient, NetworkInterfaceApiClient>(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddHttpMessageHandler<BrowserCredentialsHandler>();

        await builder.Build().RunAsync();
    }
}
