using Ufw.Web;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);

Startup.ConfigureServices(builder.Services, builder.Configuration);

WebApplication app = builder.Build();
await Startup.ConfigureAsync(app);
await app.RunAsync();
