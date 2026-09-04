using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Ufw.Client.Api;
using Ufw.Client.Auth;
using Ufw.Client.Intent;

namespace Ufw.Client;

public static class Program
{
    public static async Task Main(string[] args)
    {
        WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        string apiBaseUrl = builder.Configuration["ApiBaseUrl"]
            ?? throw new InvalidOperationException("API base URL is not configured.");
        Uri apiBaseAddress = new(apiBaseUrl, UriKind.Absolute);

        builder.Services.AddMudServices();
        builder.Services.AddAuthorizationCore();
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddScoped<AuthenticationSession>();
        builder.Services.AddScoped<IAuthenticationSession>(static services => services.GetRequiredService<AuthenticationSession>());
        builder.Services.AddScoped<AuthenticationStateProvider>(static services => services.GetRequiredService<AuthenticationSession>());
        builder.Services.AddScoped<IAuthenticationOperationCoordinator, BrowserAuthenticationOperationCoordinator>();
        builder.Services.AddScoped<BrowserCredentialsHandler>();
        builder.Services.AddScoped<BearerTokenHandler>();
        builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
        builder.Services.AddScoped<IIntentSigningService, BrowserIntentSigningService>();

        builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<BrowserCredentialsHandler>();
        // Keep browser credentials inside the bearer handler so a one-time 401 replay reapplies cookie credentials.
        builder.Services.AddHttpClient<IUfwApiClient, UfwApiClient>(client => client.BaseAddress = apiBaseAddress)
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddHttpMessageHandler<BrowserCredentialsHandler>();

        await builder.Build().RunAsync();
    }
}
