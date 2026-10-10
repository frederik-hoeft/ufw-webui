using Microsoft.AspNetCore.Components.Authorization;

namespace Ufw.Web.Client.Features.Authentication;

internal static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddBrowserAuthentication(this IServiceCollection services)
    {
        services.AddSingleton<IAccessTokenPrincipalFactory, AccessTokenPrincipalFactory>();
        // HttpClientFactory creates its own handler DI scopes. The token and principal must be shared across those scopes within this WASM app.
        services.AddSingleton<AuthenticationSession>();
        services.AddSingleton<IAuthenticationSession>(static provider => provider.GetRequiredService<AuthenticationSession>());
        services.AddSingleton<AuthenticationStateProvider>(static provider => provider.GetRequiredService<AuthenticationSession>());
        services.AddScoped<IAuthenticationOperationCoordinator, BrowserAuthenticationOperationCoordinator>();
        services.AddScoped<BrowserCredentialsHandler>();
        services.AddScoped<BearerTokenHandler>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        return services;
    }
}
