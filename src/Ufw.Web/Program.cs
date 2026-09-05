using Microsoft.Extensions.Configuration;
using Ufw.Web;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Ufw.Web intentionally has one local JSON configuration file. The committed
// appsettings.default.json is a template; environment variables and command-line
// arguments override the gitignored appsettings.json for deployed/containerized use.
builder.Configuration.Sources.Clear();
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

Startup.ConfigureServices(builder.Services, builder.Configuration);

WebApplication app = builder.Build();
await Startup.ConfigureAsync(app);
await app.RunAsync();
