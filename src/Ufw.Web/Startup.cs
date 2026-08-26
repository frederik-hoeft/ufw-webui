using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ufw.Ipc.Client.Configuration;
using Ufw.Web.Configuration;
using Ufw.Web.Configuration.Swagger;
using Ufw.Web.Data;
using Ufw.Web.Services.Auth;

namespace Ufw.Web;

internal static class Startup
{
    internal const string BLAZOR_CORS_POLICY = "BlazorClient";

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        // TODO: SQLite for dev/testing, migrate to PostgreSQL for prod
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddDatabaseDeveloperPageExceptionFilter();

        services.AddIdentityCore<IdentityUser>(options =>
            {
                options.Password.RequiredLength = 16;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = true;
            })
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
            .Bind(configuration.GetSection(RefreshTokenOptions.SectionName))
            .Validate(static options => options.Lifetime > TimeSpan.Zero, "Refresh token lifetime must be positive.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.CookieName), "Refresh token cookie name is required.")
            .Validate(static options => options.CookieName.StartsWith("__Host-", StringComparison.Ordinal), "Refresh token cookie must use the __Host- prefix.")
            .ValidateOnStart();

        services.AddSingleton<IJwtSigningKeyProvider, ECDsaJwtSigningKeyProvider>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddSingleton<IAuthenticationTimingService, PasswordHashAuthenticationTimingService>();

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
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
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

        string endpoint = configuration["IpcOptions:Endpoint"]
            ?? throw new InvalidOperationException("IPC endpoint configuration 'IpcOptions:Endpoint' not found.");
        services.AddUfwClientServices(client => client.ConnectTo(endpoint));

    }

    public static async Task ConfigureAsync(WebApplication app)
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
        app.UseCors(BLAZOR_CORS_POLICY);
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHealthChecks("/health");

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        await using ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
    }
}
