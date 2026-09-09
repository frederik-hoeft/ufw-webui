using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ufw.Ipc.Client.Configuration;
using Ufw.Web.Configuration;
using Ufw.Web.Configuration.Swagger;
using Ufw.Web.Data;
using Ufw.Web.Services.Auth;
using Ufw.Web.Services.ErrorHandling;
using Ufw.Web.Services.NetworkInterfaces;
using Wkg.AspNetCore.Configuration;
using Wkg.AspNetCore.ErrorHandling;
using Wkg.AspNetCore.Transactions;
using Wkg.AspNetCore.Transactions.Configuration;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web;

internal sealed class Startup : IAsyncStartupScript
{
    internal const string BLAZOR_CORS_POLICY = "BlazorClient";

    public static ValueTask ConfigureServicesAsync(
        IServiceCollection services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        services.AddSingleton<IModelLoader, ApplicationModelLoader>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<IErrorSentry, ApplicationErrorSentry>();
        services.AddTransactionManagement<ApplicationDbContext>(options =>
            options.UseIsolationLevel(IsolationLevel.ReadCommitted));
        services.AddDatabaseDeveloperPageExceptionFilter();

        services.Configure<IdentityOptions>(configuration.GetSection("Auth:Identity"));
        services.AddIdentityCore<IdentityUser>()
            .AddRoles<IdentityRole>()
            .AddSignInManager()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SECTION_NAME))
            .Validate(static options => !string.IsNullOrWhiteSpace(options.Issuer), "JWT issuer is required.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.Audience), "JWT audience is required.")
            .Validate(static options => options.AccessTokenLifetime > TimeSpan.Zero, "JWT access token lifetime must be positive.")
            .Validate(static options => options.ClockSkew >= TimeSpan.Zero, "JWT clock skew cannot be negative.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.SigningKeyPath), "JWT signing key path is required.")
            .Validate(static options => File.Exists(options.SigningKeyPath), "JWT signing key file does not exist.")
            .ValidateOnStart();

        services.AddOptions<RefreshTokenOptions>()
            .Bind(configuration.GetSection(RefreshTokenOptions.SECTION_NAME))
            .Validate(static options => options.Lifetime > TimeSpan.Zero, "Refresh token lifetime must be positive.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.CookieName), "Refresh token cookie name is required.")
            .Validate(static options => options.CookieName.StartsWith("__Host-", StringComparison.Ordinal), "Refresh token cookie must use the __Host- prefix.")
            .ValidateOnStart();

        services.AddOptions<AuthenticationBootstrapOptions>()
            .Bind(configuration.GetSection(AuthenticationBootstrapOptions.SECTION_NAME))
            .Validate(static options => options.IsValid(), "Authentication bootstrap user configuration is invalid or contains duplicate identities.")
            .ValidateOnStart();

        services.AddSingleton<IJwtSigningKeyProvider, ECDsaJwtSigningKeyProvider>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<AuthenticationBootstrapService>();
        services.AddSingleton<IAuthenticationTimingService, PasswordHashAuthenticationTimingService>();
        services.AddScoped<INetworkInterfaceInventoryService, NetworkInterfaceInventoryService>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IJwtSigningKeyProvider, IOptions<JwtOptions>>((options, signingKeyProvider, jwtOptions) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Value.Issuer,
                    ValidAudience = jwtOptions.Value.Audience,
                    IssuerSigningKey = signingKeyProvider.SigningKey,
                    ValidAlgorithms = [signingKeyProvider.SigningAlgorithm],
                    ClockSkew = jwtOptions.Value.ClockSkew,
                    NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                };
            });

        services.AddAuthorization();
        services.AddProblemDetails();
        services.AddControllers();
        services.AddApiVersioning(options =>
            {
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });
        services.AddSwaggerGen();
        services.ConfigureOptions<ConfigureSwaggerOptions>();
        services.AddHealthChecks();

        string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy(BLAZOR_CORS_POLICY, policy =>
        {
            if (allowedOrigins.Length == 0)
            {
                return;
            }

            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }));

        services.AddOptions<IpcClientOptions>()
            .Bind(configuration.GetSection(IpcClientOptions.SECTION_NAME))
            .Validate(static options => options.IsValid(), "IPC endpoint/TLS configuration is invalid.")
            .ValidateOnStart();

        IpcClientOptions ipcOptions = configuration.GetSection(IpcClientOptions.SECTION_NAME).Get<IpcClientOptions>()
            ?? throw new InvalidOperationException("IPC endpoint configuration 'IpcOptions' was not found.");
        if (!ipcOptions.IsValid())
        {
            throw new InvalidOperationException("IPC endpoint/TLS configuration is invalid.");
        }

        services.AddUfwClientServices(client =>
        {
            client.ConnectTo(ipcOptions.Endpoint)
                .UseRequestTimeout(ipcOptions.RequestTimeout);
            if (ipcOptions.TlsEnabled)
            {
                client.UseSsl(ipcOptions.TlsServerName!, ipcOptions.SslProtocols);
            }

            if (!string.IsNullOrWhiteSpace(ipcOptions.ClientCertificatePath)
                && !string.IsNullOrWhiteSpace(ipcOptions.ClientCertificateKeyPath))
            {
                client.UseClientCertificate(ipcOptions.ClientCertificatePath, ipcOptions.ClientCertificateKeyPath);
            }
        });

        return ValueTask.CompletedTask;
    }

    public static async ValueTask ConfigureAsync(
        WebApplication app,
        CancellationToken cancellationToken = default)
    {
        _ = app.Services.GetRequiredService<IJwtSigningKeyProvider>();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseMigrationsEndPoint();
            IApiVersionDescriptionProvider versionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                foreach (ApiVersionDescription description in versionProvider.ApiVersionDescriptions)
                {
                    options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
                }
            });
        }
        else
        {
            app.UseExceptionHandler();
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseCors(BLAZOR_CORS_POLICY);
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHealthChecks("/health");

        string clientIndexPath = Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html");
        if (File.Exists(clientIndexPath))
        {
            app.MapFallbackToFile("index.html");
        }

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync(cancellationToken);

        AuthenticationBootstrapService bootstrapService = scope.ServiceProvider.GetRequiredService<AuthenticationBootstrapService>();
        ITransactionService<ApplicationDbContext> transactionService =
            scope.ServiceProvider.GetRequiredService<ITransactionService<ApplicationDbContext>>();
        await transactionService.Scoped.RunAsync(async (_, transaction, ct) =>
        {
            await bootstrapService.ApplyAsync(ct);
            return transaction.Commit();
        }, cancellationToken);
    }
}
