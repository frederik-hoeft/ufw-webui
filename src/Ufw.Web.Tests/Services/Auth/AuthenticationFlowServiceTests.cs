using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Data;
using Ufw.Web.Data;
using Ufw.Web.Services.Auth;
using Wkg.AspNetCore.Transactions;
using Wkg.AspNetCore.Transactions.Configuration;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web.Tests.Services.Auth;

[TestClass]
public sealed class AuthenticationFlowServiceTests
{
    private const string EMAIL = "operator@example.invalid";
    private const string PASSWORD = "correct-password";

    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task LoginAsync_ValidCredentials_IssuesAccessAndRefreshTokensAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        IdentityUser user = await host.CreateUserAsync(EMAIL, PASSWORD);
        AccessToken accessToken = new("access-token", DateTimeOffset.UtcNow.AddMinutes(5));
        RefreshTokenIssueResult refreshToken = new("refresh-token", DateTimeOffset.UtcNow.AddDays(1));
        host.JwtTokens.Setup(tokens => tokens.IssueAsync(user, It.IsAny<CancellationToken>())).ReturnsAsync(accessToken);
        host.RefreshTokens.Setup(tokens => tokens.IssueAsync(user, It.IsAny<CancellationToken>())).ReturnsAsync(refreshToken);

        AuthenticationTokenResult? result = await host.Service.LoginAsync(EMAIL, PASSWORD, TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.AreEqual(accessToken, result.AccessToken);
        Assert.AreEqual(refreshToken.Token, result.RefreshToken);
        Assert.AreEqual(refreshToken.ExpiresAt, result.RefreshTokenExpiresAt);
        host.AuthenticationTiming.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task LoginAsync_UnknownUser_PerformsDummyVerificationAndDoesNotIssueTokensAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);

        AuthenticationTokenResult? result = await host.Service.LoginAsync(EMAIL, PASSWORD, TestContext.CancellationToken);

        Assert.IsNull(result);
        host.AuthenticationTiming.Verify(service => service.PerformDummyPasswordVerification(PASSWORD), Times.Once);
        host.JwtTokens.VerifyNoOtherCalls();
        host.RefreshTokens.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task LoginAsync_InvalidPassword_CommitsIdentityFailureStateAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        IdentityUser user = await host.CreateUserAsync(EMAIL, PASSWORD);

        AuthenticationTokenResult? result = await host.Service.LoginAsync(EMAIL, "wrong-password", TestContext.CancellationToken);

        Assert.IsNull(result);
        IdentityUser? updated = await host.UserManager.FindByIdAsync(user.Id);
        Assert.IsNotNull(updated);
        Assert.AreEqual(1, updated.AccessFailedCount);
        host.JwtTokens.VerifyNoOtherCalls();
        host.RefreshTokens.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RefreshAsync_ValidRotation_IssuesReplacementAccessTokenAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        IdentityUser user = await host.CreateUserAsync(EMAIL, PASSWORD);
        DateTimeOffset refreshExpiry = DateTimeOffset.UtcNow.AddDays(1);
        RefreshTokenRotationResult rotation = new(user, "replacement-refresh-token", refreshExpiry);
        AccessToken accessToken = new("replacement-access-token", DateTimeOffset.UtcNow.AddMinutes(5));
        host.RefreshTokens.Setup(tokens => tokens.RotateAsync("refresh-token", It.IsAny<CancellationToken>())).ReturnsAsync(rotation);
        host.JwtTokens.Setup(tokens => tokens.IssueAsync(user, It.IsAny<CancellationToken>())).ReturnsAsync(accessToken);

        AuthenticationTokenResult? result = await host.Service.RefreshAsync("refresh-token", TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.AreEqual(accessToken, result.AccessToken);
        Assert.AreEqual(rotation.Token, result.RefreshToken);
        Assert.AreEqual(refreshExpiry, result.RefreshTokenExpiresAt);
    }

    [TestMethod]
    public async Task RefreshAsync_RejectedRotation_ReturnsUnauthorizedOutcomeWithoutIssuingAccessTokenAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.RefreshTokens.Setup(tokens => tokens.RotateAsync("replayed-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshTokenRotationResult?)null);

        AuthenticationTokenResult? result = await host.Service.RefreshAsync("replayed-token", TestContext.CancellationToken);

        Assert.IsNull(result);
        host.JwtTokens.VerifyNoOtherCalls();
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _services;
        private readonly AsyncServiceScope _scope;

        private TestHost(
            SqliteConnection connection,
            ServiceProvider services,
            AsyncServiceScope scope,
            UserManager<IdentityUser> userManager,
            AuthenticationFlowService service,
            Mock<IJwtTokenService> jwtTokens,
            Mock<IRefreshTokenService> refreshTokens,
            Mock<IAuthenticationTimingService> authenticationTiming)
        {
            _connection = connection;
            _services = services;
            _scope = scope;
            UserManager = userManager;
            Service = service;
            JwtTokens = jwtTokens;
            RefreshTokens = refreshTokens;
            AuthenticationTiming = authenticationTiming;
        }

        public UserManager<IdentityUser> UserManager { get; }

        public AuthenticationFlowService Service { get; }

        public Mock<IJwtTokenService> JwtTokens { get; }

        public Mock<IRefreshTokenService> RefreshTokens { get; }

        public Mock<IAuthenticationTimingService> AuthenticationTiming { get; }

        public static async Task<TestHost> CreateAsync(CancellationToken cancellationToken)
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync(cancellationToken);

            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton<IModelLoader, ApplicationModelLoader>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddHttpContextAccessor();
            services.AddAuthentication();
            services.AddIdentityCore<IdentityUser>(options =>
                {
                    options.Password.RequiredLength = 5;
                    options.Password.RequireDigit = false;
                    options.Password.RequireLowercase = false;
                    options.Password.RequireUppercase = false;
                    options.Password.RequireNonAlphanumeric = false;
                    options.User.RequireUniqueEmail = true;
                })
                .AddSignInManager()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddTransactionManagement<ApplicationDbContext>(options => options.UseIsolationLevel(IsolationLevel.ReadCommitted));

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);

            Mock<IJwtTokenService> jwtTokens = new();
            Mock<IRefreshTokenService> refreshTokens = new();
            Mock<IAuthenticationTimingService> authenticationTiming = new();
            UserManager<IdentityUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            SignInManager<IdentityUser> signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<IdentityUser>>();
            ITransactionServiceHandle transactionHandle = scope.ServiceProvider.GetRequiredService<ITransactionServiceHandle>();
            AuthenticationFlowService service = new(userManager, signInManager, jwtTokens.Object, refreshTokens.Object, authenticationTiming.Object, transactionHandle);

            return new TestHost(connection, serviceProvider, scope, userManager, service, jwtTokens, refreshTokens, authenticationTiming);
        }

        public async Task<IdentityUser> CreateUserAsync(string email, string password)
        {
            IdentityUser user = new()
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                LockoutEnabled = true,
            };
            IdentityResult result = await UserManager.CreateAsync(user, password);
            Assert.IsTrue(result.Succeeded, string.Join("; ", result.Errors.Select(static error => error.Description)));
            return user;
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _services.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
