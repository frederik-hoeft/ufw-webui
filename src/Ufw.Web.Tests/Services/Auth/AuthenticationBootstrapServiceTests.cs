using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ufw.Web.Configuration;
using Ufw.Web.Data;
using Ufw.Web.Services.Auth;

namespace Ufw.Web.Tests.Services.Auth;

[TestClass]
public sealed class AuthenticationBootstrapServiceTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ApplyAsync_MissingUser_CreatesConfirmedUserAsync()
    {
        await using TestIdentityHost host = await TestIdentityHost.CreateAsync(TestContext.CancellationToken);
        AuthenticationBootstrapOptions options = CreateOptions(
            email: "bootstrap@example.invalid",
            password: "BootstrapPassword123");

        await host.ApplyAsync(options, TestContext.CancellationToken);

        IdentityUser? user = await host.UserManager.FindByEmailAsync("bootstrap@example.invalid");
        Assert.IsNotNull(user);
        Assert.AreEqual("bootstrap@example.invalid", user.UserName);
        Assert.IsTrue(user.EmailConfirmed);
        Assert.IsTrue(user.LockoutEnabled);
        Assert.IsTrue(await host.UserManager.CheckPasswordAsync(user, "BootstrapPassword123"));
    }

    [TestMethod]
    public async Task ApplyAsync_ExistingUser_DoesNotResetPasswordAsync()
    {
        await using TestIdentityHost host = await TestIdentityHost.CreateAsync(TestContext.CancellationToken);
        AuthenticationBootstrapOptions initialOptions = CreateOptions(
            email: "bootstrap@example.invalid",
            password: "InitialPassword1234",
            emailConfirmed: false);
        await host.ApplyAsync(initialOptions, TestContext.CancellationToken);

        AuthenticationBootstrapOptions secondOptions = CreateOptions(
            email: "bootstrap@example.invalid",
            password: "ReplacementPassword123",
            emailConfirmed: true);
        await host.ApplyAsync(secondOptions, TestContext.CancellationToken);

        IdentityUser? user = await host.UserManager.FindByEmailAsync("bootstrap@example.invalid");
        Assert.IsNotNull(user);
        Assert.IsTrue(user.EmailConfirmed);
        Assert.IsTrue(await host.UserManager.CheckPasswordAsync(user, "InitialPassword1234"));
        Assert.IsFalse(await host.UserManager.CheckPasswordAsync(user, "ReplacementPassword123"));
    }

    [TestMethod]
    public async Task ApplyAsync_ExistingUser_DoesNotRequireConfiguredPasswordAsync()
    {
        await using TestIdentityHost host = await TestIdentityHost.CreateAsync(TestContext.CancellationToken);
        AuthenticationBootstrapOptions initialOptions = CreateOptions(
            email: "bootstrap@example.invalid",
            password: "InitialPassword1234",
            emailConfirmed: false);
        await host.ApplyAsync(initialOptions, TestContext.CancellationToken);

        AuthenticationBootstrapOptions secondOptions = CreateOptions(
            email: "bootstrap@example.invalid",
            password: null,
            emailConfirmed: true);
        await host.ApplyAsync(secondOptions, TestContext.CancellationToken);

        IdentityUser? user = await host.UserManager.FindByEmailAsync("bootstrap@example.invalid");
        Assert.IsNotNull(user);
        Assert.IsTrue(user.EmailConfirmed);
    }

    [TestMethod]
    public async Task ApplyAsync_MissingUserWithoutPassword_ThrowsAsync()
    {
        await using TestIdentityHost host = await TestIdentityHost.CreateAsync(TestContext.CancellationToken);
        AuthenticationBootstrapOptions options = CreateOptions(
            email: "bootstrap@example.invalid",
            password: null);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => host.ApplyAsync(options, TestContext.CancellationToken));

        StringAssert.Contains(exception.Message, "requires a password");
        Assert.IsNull(await host.UserManager.FindByEmailAsync("bootstrap@example.invalid"));
    }

    [TestMethod]
    public void IsValid_DuplicateEffectiveUserName_ReturnsFalse()
    {
        AuthenticationBootstrapOptions options = new();
        options.Users.Add(new BootstrapUserOptions { Email = "first@example.invalid" });
        options.Users.Add(new BootstrapUserOptions
        {
            Email = "second@example.invalid",
            UserName = "FIRST@example.invalid",
        });

        Assert.IsFalse(options.IsValid());
    }

    private static AuthenticationBootstrapOptions CreateOptions(
        string email,
        string? password,
        bool emailConfirmed = true)
    {
        AuthenticationBootstrapOptions options = new();
        options.Users.Add(new BootstrapUserOptions
        {
            Email = email,
            Password = password,
            EmailConfirmed = emailConfirmed,
        });
        return options;
    }

    private sealed class TestIdentityHost : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _serviceProvider;
        private readonly AsyncServiceScope _scope;

        private TestIdentityHost(
            SqliteConnection connection,
            ServiceProvider serviceProvider,
            AsyncServiceScope scope,
            UserManager<IdentityUser> userManager)
        {
            _connection = connection;
            _serviceProvider = serviceProvider;
            _scope = scope;
            UserManager = userManager;
        }

        public UserManager<IdentityUser> UserManager { get; }

        public static async Task<TestIdentityHost> CreateAsync(CancellationToken cancellationToken)
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync(cancellationToken);

            ServiceCollection services = new();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<IdentityUser>(options =>
                {
                    options.Password.RequiredLength = 16;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = false;
                    options.User.RequireUniqueEmail = true;
                })
                .AddEntityFrameworkStores<ApplicationDbContext>();

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);
            UserManager<IdentityUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

            return new TestIdentityHost(connection, serviceProvider, scope, userManager);
        }

        public Task ApplyAsync(AuthenticationBootstrapOptions options, CancellationToken cancellationToken)
        {
            AuthenticationBootstrapService service = new(
                UserManager,
                Options.Create(options),
                NullLogger<AuthenticationBootstrapService>.Instance);
            return service.ApplyAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _serviceProvider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
