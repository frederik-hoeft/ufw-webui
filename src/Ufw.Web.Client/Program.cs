using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Configuration;
using Ufw.Web.Client.Features.Authentication;
using Ufw.Web.Client.Features.KnownHosts;
using Ufw.Web.Client.Features.NetworkInterfaces;
using Ufw.Web.Client.Features.Rules.Intent;
using Ufw.Web.Client.Features.Rules.Services;
using Ufw.Web.Client.Features.Rules.Templates;
using Ufw.Web.Client.Features.Status;
using Ufw.Web.Client.Services.Clipboard;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Client.Services.Localization;
using Ufw.Web.Client.Services.Storage;
using Ufw.Web.Client.Services.Theming;
using Ufw.Web.Client.UI;
using Ufw.Web.Client.UI.Components.Hosts;
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

        builder.Services.AddBrowserAuthentication();
        builder.Services.AddScoped<IClientErrorMapper, ClientErrorMapper>();
        builder.Services.AddScoped<IBrowserIntentCryptoService, BrowserIntentCryptoService>();
        builder.Services.AddScoped<IIntentSigningService, BrowserIntentSigningService>();
        builder.Services.AddScoped<IClientThemeService, BrowserClientThemeService>();
        builder.Services.AddScoped<IKnownHostInventoryService, KnownHostInventoryService>();
        builder.Services.AddScoped<IKnownHostEditorDialogService, KnownHostEditorDialogService>();
        builder.Services.AddSingleton<IKnownHostSuggestionService, KnownHostSuggestionService>();
        builder.Services.AddScoped<INetworkInterfaceInventoryService, NetworkInterfaceInventoryService>();
        builder.Services.AddScoped<IOperationalStatusService, OperationalStatusService>();
        builder.Services.AddScoped<IRuleTemplateCatalogService, RuleTemplateCatalogService>();
        builder.Services.AddSingleton<IRuleTemplateDraftFactory, RuleTemplateDraftFactory>();
        builder.Services.AddSingleton<IRuleTemplateAuthoringService, RuleTemplateAuthoringService>();

        builder.Services.AddClientApiServices();

        await builder.Build().RunAsync();
    }
}
