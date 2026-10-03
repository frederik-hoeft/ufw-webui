using Ufw.Web.Data.Access.KnownHosts;
using Ufw.Web.Data.Access.NetworkInterfaces;
using Ufw.Web.Data.Access.Rules.Metadata;
using Ufw.Web.Data.Access.Rules;
using Ufw.Web.Data.Access.Rules.Groups;
using Ufw.Web.Data.Access.Rules.Tags;
using Ufw.Web.Data.Access.Rules.Templates;
using Microsoft.AspNetCore.Identity;
using Ufw.Shared.Web;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using Ufw.Ipc.Client;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Configuration;
using Ufw.Web.Data;
using Ufw.Web.Services.Auth;
using Ufw.Web.Services.KnownHosts;
using Ufw.Web.Services.NetworkInterfaces;
using Ufw.Web.Services.Rules;
using Ufw.Web.Tests.Integration.Support;
using Wkg.AspNetCore.TestAdapters.Initialization;
using Wkg.AspNetCore.TestAdapters.Initialization.Extensions;
using Wkg.AspNetCore.Transactions.Configuration;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web.Tests.Integration;

public sealed class IntegrationTestInitializer : IAsyncDITestInitializer
{
    public static ValueTask ConfigureAsync(IServiceCollection services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        cancellationToken.ThrowIfCancellationRequested();

        services.AddLogging();
        services.AddAntiforgery(options => options.HeaderName = BrowserRequestHeaders.CSRF_TOKEN);
        services.AddControllers();
        services.AddSingleton<IModelLoader, SqliteApplicationModelLoader>();
        services.AddSingleton(static _ => new SqliteConnection("Data Source=:memory:"));
        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
            options.UseSqlite(serviceProvider.GetRequiredService<SqliteConnection>()));
        services.AddTransactionManagement<ApplicationDbContext>(options =>
            options.UseIsolationLevel(IsolationLevel.ReadCommitted));
        services.MockDatabaseTransactions<ApplicationDbContext>();

        services.AddHttpContextAccessor();
        services.AddAuthentication();
        services.AddIdentityCore<IdentityUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddSignInManager()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.Configure<JwtOptions>(options =>
        {
            options.Issuer = "ufw-webui-integration-tests";
            options.Audience = "ufw-webui-integration-tests";
            options.AccessTokenLifetime = TimeSpan.FromMinutes(5);
        });
        services.Configure<RefreshTokenOptions>(options => options.Lifetime = TimeSpan.FromDays(30));

        services.AddScoped<IntegrationTimeProvider>();
        services.AddScoped<TimeProvider>(static serviceProvider => serviceProvider.GetRequiredService<IntegrationTimeProvider>());
        services.AddSingleton<IJwtSigningKeyProvider, IntegrationJwtSigningKeyProvider>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddSingleton<IAuthenticationTimingService, PasswordHashAuthenticationTimingService>();
        services.AddScoped<AuthenticationFlowService>();
        services.AddScoped<AuthController>();
        services.AddScoped<IAuthenticationFlowService>(static serviceProvider => serviceProvider.GetRequiredService<AuthenticationFlowService>());

        services.AddScoped<KnownHostDataAccess>();
        services.AddScoped<IntegrationKnownHostDnsResolver>();
        services.AddScoped<IKnownHostDnsResolver>(static serviceProvider => serviceProvider.GetRequiredService<IntegrationKnownHostDnsResolver>());
        services.AddScoped<IKnownHostDataAccess>(static serviceProvider => serviceProvider.GetRequiredService<KnownHostDataAccess>());
        services.AddScoped<KnownHostService>();
        services.AddScoped<IKnownHostService>(static serviceProvider => serviceProvider.GetRequiredService<KnownHostService>());
        services.AddScoped<KnownHostsController>();

        services.AddScoped<IntegrationNetworkInterfaceDaemonGateway>();
        services.AddScoped<INetworkInterfaceDaemonGateway>(static serviceProvider => serviceProvider.GetRequiredService<IntegrationNetworkInterfaceDaemonGateway>());
        services.AddScoped<NetworkInterfaceDataAccess>();
        services.AddScoped<INetworkInterfaceDataAccess>(static serviceProvider => serviceProvider.GetRequiredService<NetworkInterfaceDataAccess>());
        services.AddScoped<NetworkInterfaceInventoryService>();
        services.AddScoped<INetworkInterfaceInventoryService>(static serviceProvider => serviceProvider.GetRequiredService<NetworkInterfaceInventoryService>());
        services.AddScoped<IntegrationUfwClient>();
        services.AddScoped<IUfwClient>(static serviceProvider => serviceProvider.GetRequiredService<IntegrationUfwClient>());
        services.AddScoped<IRuleDaemonGateway, RuleDaemonGateway>();
        services.AddScoped<IRuleMetadataDataAccess, RuleMetadataDataAccess>();
        services.AddSingleton<IRuleMetadataValuesNormalizer, RuleMetadataValuesNormalizer>();
        services.AddScoped<IRuleGroupDataAccess, RuleGroupDataAccess>();
        services.AddScoped<IRuleTagDataAccess, RuleTagDataAccess>();
        services.AddScoped<IRuleTemplateDataAccess, RuleTemplateDataAccess>();
        services.AddScoped<IRuleInventoryService, RuleInventoryService>();
        services.AddScoped<IRuleMetadataService, RuleMetadataService>();
        services.AddScoped<IRuleMetadataReconciliationService, RuleMetadataReconciliationService>();
        services.AddScoped<NetworkInterfacesController>();
        services.AddScoped<RulesController>();
        services.AddScoped<RuleMetadataController>();
        services.AddScoped<RuleGroupsController>();
        services.AddScoped<RuleTagsController>();
        services.AddScoped<RuleTemplatesController>();

        return ValueTask.CompletedTask;
    }

    public static async ValueTask InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        SqliteConnection connection = serviceProvider.GetRequiredService<SqliteConnection>();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);
    }
}
