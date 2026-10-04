using Ufw.Web.Data.Access.Auth;
using Ufw.Web.Data.Access.KnownHosts;
using Ufw.Web.Data.Access.NetworkInterfaces;
using Ufw.Web.Data.Access.Rules.Groups;
using Ufw.Web.Data.Access.Rules.Metadata;
using Ufw.Web.Data.Access.Rules.Tags;
using Ufw.Web.Data.Access.Rules.Templates;
using Ufw.Web.Security;
using Ufw.Web.Services.Auth;
using Ufw.Web.Services.Intent;
using Ufw.Web.Services.KnownHosts;
using Ufw.Web.Services.NetworkInterfaces;
using Ufw.Web.Services.Rules;
using Ufw.Web.Services.Status;

namespace Ufw.Web;

internal static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Authentication
        services.AddSingleton<IJwtSigningKeyProvider, ECDsaJwtSigningKeyProvider>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenDataAccess, RefreshTokenDataAccess>();
        services.AddScoped<IAuthenticationFlowService, AuthenticationFlowService>();
        services.AddScoped<AuthenticationBootstrapService>();
        services.AddSingleton<IAuthenticationTimingService, PasswordHashAuthenticationTimingService>();

        // Known hosts
        services.AddScoped<IKnownHostDataAccess, KnownHostDataAccess>();
        services.AddSingleton<IKnownHostDnsResolver, KnownHostDnsResolver>();
        services.AddScoped<IKnownHostService, KnownHostService>();

        // Network interfaces
        services.AddScoped<INetworkInterfaceDaemonGateway, NetworkInterfaceDaemonGateway>();
        services.AddScoped<INetworkInterfaceDataAccess, NetworkInterfaceDataAccess>();
        services.AddScoped<INetworkInterfaceInventoryService, NetworkInterfaceInventoryService>();

        // Rule management
        services.AddScoped<IRuleDaemonGateway, RuleDaemonGateway>();
        services.AddScoped<IRuleMetadataDataAccess, RuleMetadataDataAccess>();
        services.AddScoped<IRuleGroupDataAccess, RuleGroupDataAccess>();
        services.AddScoped<IRuleTagDataAccess, RuleTagDataAccess>();
        services.AddScoped<IRuleTemplateDataAccess, RuleTemplateDataAccess>();
        services.AddScoped<IRuleInventoryService, RuleInventoryService>();
        services.AddScoped<IRuleMetadataService, RuleMetadataService>();
        services.AddScoped<IRuleMetadataReconciliationService, RuleMetadataReconciliationService>();

        // Daemon-backed application endpoints
        services.AddScoped<IStatusDaemonGateway, StatusDaemonGateway>();
        services.AddScoped<IIntentDaemonGateway, IntentDaemonGateway>();

        return services;
    }
}
