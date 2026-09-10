using Microsoft.Extensions.DependencyInjection;

namespace Ufw.Client.Localization;

internal static class ClientLocalizationServiceCollectionExtensions
{
    public static IServiceCollection AddClientLocalization(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        ClientLocalizationOptions options = ClientLocalizationOptions.FromConfiguration(configuration);
        services.AddSingleton(options);
        services.AddLocalization(localization => localization.ResourcesPath = "Resources");
        services.AddScoped<IClientCultureService, BrowserClientCultureService>();
        return services;
    }
}
