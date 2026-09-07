using Ufw.Web;
using Wkg.AspNetCore.Configuration;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.Sources.Clear();
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

WebApplication app = await builder.BuildUsingAsync<Startup>();
await app.RunAsync();
