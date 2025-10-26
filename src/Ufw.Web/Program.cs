using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ufw.Ipc.Client.Configuration;
using Ufw.Ipc.Shared.Transport.Security;
using Ufw.Web.Data;
using Ufw.Web.Pipeline;
using Ufw.Web.Pipeline.Normalizers;
using Ufw.Web.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Register UFW Rule Service
builder.Services.AddScoped<IUfwRuleService, UfwRuleService>();

// Register Network Interface Service
builder.Services.AddScoped<INetworkInterfaceService, NetworkInterfaceService>();

// Register Display Service
builder.Services.AddScoped<IUfwDisplayService, UfwDisplayService>();

// Register normalization pipeline
builder.Services.AddScoped<IRuleNormalizer, TrimWhitespaceNormalizer>();
builder.Services.AddScoped<IRuleNormalizer, AnyValueNormalizer>();
builder.Services.AddScoped<IRuleNormalizer, PortRangeNormalizer>();
builder.Services.AddScoped<IRuleNormalizationService, RuleNormalizationService>();

string endpoint = builder.Configuration["IpcOptions:Endpoint"]
    ?? throw new InvalidOperationException("IPC endpoint configuration 'IpcOptions:Endpoint' not found.");

builder.Services.AddUfwClientServices(client => client.ConnectTo(endpoint));
// TODO: temp for testing without TLS
builder.Services.AddSingleton<ITransportSecurityService, NoTransportSecurityService>();

builder.Services.AddRazorPages();

WebApplication app = builder.Build();

// Ensure database is created and migrated
using (IServiceScope scope = app.Services.CreateScope())
{
    IServiceProvider services = scope.ServiceProvider;
    ApplicationDbContext context = services.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
