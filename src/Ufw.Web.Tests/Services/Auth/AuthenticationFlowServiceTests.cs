using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using System.Data;
using Ufw.Web.Configuration;
using Ufw.Web.Data;
using Ufw.Web.Data.Access.Auth;
using Ufw.Web.Data.Model;
using Ufw.Web.Services.Auth;
using Ufw.Web.Tests.Data;
using Wkg.AspNetCore.Exceptions;
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
        host.RefreshTokens.IssueHandler = (context, userId, securityStamp, _) =>
        {
            Assert.AreSame(host.Context, context);
            Assert.AreEqual(user.Id, userId);
            Assert.AreEqual(user.SecurityStamp, securityStamp);
            return Task.FromResult(refreshToken);
        };

        AuthenticationTokenResult? result = await host.Service.LoginAsync(EMAIL, PASSWORD, TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.AreEqual(accessToken, result.AccessToken);
        Assert.AreEqual(refreshToken.Token, result.RefreshToken);
        Assert.AreEqual(refreshToken.ExpiresAt, result.RefreshTokenExpiresAt);
        Assert.AreEqual(1, host.RefreshTokens.IssueCalls);
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
        host.RefreshTokens.AssertNoCalls();
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
        host.RefreshTokens.AssertNoCalls();
    }

    [TestMethod]
    public async Task ChangePasswordAsync_ValidPassword_RotatesCredentialsAndSessionAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        IdentityUser user = await host.CreateUserAsync(EMAIL, PASSWORD);
        AccessToken accessToken = new("replacement-access-token", DateTimeOffset.UtcNow.AddMinutes(5));
        RefreshTokenIssueResult refreshToken = new("replacement-refresh-token", DateTimeOffset.UtcNow.AddDays(1));
        host.RefreshTokens.RevokeUserHandler = (context, userId, _) =>
        {
            Assert.AreSame(host.Context, context);
            Assert.AreEqual(user.Id, userId);
            return Task.CompletedTask;
        };
        host.JwtTokens.Setup(tokens => tokens.IssueAsync(user, It.IsAny<CancellationToken>())).ReturnsAsync(accessToken);
        host.RefreshTokens.IssueHandler = (_, userId, securityStamp, _) =>
        {
            Assert.AreEqual(user.Id, userId);
            Assert.AreEqual(user.SecurityStamp, securityStamp);
            return Task.FromResult(refreshToken);
        };

        PasswordChangeResult? result = await host.Service.ChangePasswordAsync(user.Id, PASSWORD, "replacement-password", TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.Succeeded);
        Assert.IsNotNull(result.Authentication);
        Assert.AreEqual(accessToken, result.Authentication.AccessToken);
        Assert.IsTrue(await host.UserManager.CheckPasswordAsync(user, "replacement-password"));
        Assert.IsFalse(await host.UserManager.CheckPasswordAsync(user, PASSWORD));
        Assert.AreEqual(1, host.RefreshTokens.RevokeUserCalls);
        Assert.AreEqual(1, host.RefreshTokens.IssueCalls);
    }

    [TestMethod]
    public async Task ChangePasswordAsync_WrongCurrentPassword_DoesNotRotateSessionAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        IdentityUser user = await host.CreateUserAsync(EMAIL, PASSWORD);

        PasswordChangeResult? result = await host.Service.ChangePasswordAsync(user.Id, "wrong-password", "replacement-password", TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.IsFalse(result.Succeeded);
        Assert.HasCount(1, result.ValidationErrors);
        Assert.AreEqual(PasswordChangeValidationField.CurrentPassword, result.ValidationErrors[0].Field);
        Assert.IsNull(result.Authentication);
        Assert.IsTrue(await host.UserManager.CheckPasswordAsync(user, PASSWORD));
        host.JwtTokens.VerifyNoOtherCalls();
        host.RefreshTokens.AssertNoCalls();
    }

    [TestMethod]
    public async Task ChangePasswordAsync_WhenTokenIssuanceFails_RollsBackIdentityAndRefreshTokenChangesAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        IdentityUser user = await host.CreateUserAsync(EMAIL, PASSWORD);
        RefreshTokenDataAccess refreshTokens = new(Options.Create(new RefreshTokenOptions { Lifetime = TimeSpan.FromDays(1) }), TimeProvider.System);
        _ = await refreshTokens.IssueAsync(host.Context, user.Id, user.SecurityStamp, TestContext.CancellationToken);

        Mock<IJwtTokenService> failingJwtTokens = new();
        failingJwtTokens.Setup(tokens => tokens.IssueAsync(user, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Token issuance failed."));
        AuthenticationFlowService service = new(
            host.UserManager,
            host.SignInManager,
            failingJwtTokens.Object,
            refreshTokens,
            host.AuthenticationTiming.Object,
            host.TransactionHandle);

        ApiProxyException exception = await Assert.ThrowsExactlyAsync<ApiProxyException>(() =>
            service.ChangePasswordAsync(user.Id, PASSWORD, "replacement-password", TestContext.CancellationToken));
        Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
        await host.TransactionService.Scoped.DisposeAsync();

        host.Context.ChangeTracker.Clear();
        IdentityUser? reloaded = await host.UserManager.FindByIdAsync(user.Id);
        Assert.IsNotNull(reloaded);
        Assert.IsTrue(await host.UserManager.CheckPasswordAsync(reloaded, PASSWORD));
        Assert.IsFalse(await host.UserManager.CheckPasswordAsync(reloaded, "replacement-password"));
        Assert.AreEqual(1, await host.Context.Set<RefreshToken>().CountAsync(token => token.UserId == user.Id && token.RevokedAt == null, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task RefreshAsync_ValidRotation_IssuesReplacementAccessTokenAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        IdentityUser user = await host.CreateUserAsync(EMAIL, PASSWORD);
        DateTimeOffset refreshExpiry = DateTimeOffset.UtcNow.AddDays(1);
        RefreshTokenRotationResult rotation = new(user.Id, "replacement-refresh-token", refreshExpiry);
        AccessToken accessToken = new("replacement-access-token", DateTimeOffset.UtcNow.AddMinutes(5));
        host.RefreshTokens.RotateHandler = (context, token, _) =>
        {
            Assert.AreSame(host.Context, context);
            Assert.AreEqual("refresh-token", token);
            return Task.FromResult<RefreshTokenRotationResult?>(rotation);
        };
        host.JwtTokens.Setup(tokens => tokens.IssueAsync(user, It.IsAny<CancellationToken>())).ReturnsAsync(accessToken);

        AuthenticationTokenResult? result = await host.Service.RefreshAsync("refresh-token", TestContext.CancellationToken);

        Assert.IsNotNull(result);
        Assert.AreEqual(accessToken, result.AccessToken);
        Assert.AreEqual(rotation.Token, result.RefreshToken);
        Assert.AreEqual(refreshExpiry, result.RefreshTokenExpiresAt);
    }

    [TestMethod]
    public async Task RefreshAsync_WhenRotatedUserNoLongerExists_RevokesReplacementFamilyAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        RefreshTokenRotationResult rotation = new("missing-user", "replacement-refresh-token", DateTimeOffset.UtcNow.AddDays(1));
        host.RefreshTokens.RotateHandler = (_, _, _) => Task.FromResult<RefreshTokenRotationResult?>(rotation);
        host.RefreshTokens.RevokeFamilyHandler = (context, token, _) =>
        {
            Assert.AreSame(host.Context, context);
            Assert.AreEqual(rotation.Token, token);
            return Task.CompletedTask;
        };

        AuthenticationTokenResult? result = await host.Service.RefreshAsync("refresh-token", TestContext.CancellationToken);

        Assert.IsNull(result);
        Assert.AreEqual(1, host.RefreshTokens.RevokeFamilyCalls);
        host.JwtTokens.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RefreshAsync_RejectedRotation_ReturnsUnauthorizedOutcomeWithoutIssuingAccessTokenAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.RefreshTokens.RotateHandler = (_, _, _) => Task.FromResult<RefreshTokenRotationResult?>(null);

        AuthenticationTokenResult? result = await host.Service.RefreshAsync("replayed-token", TestContext.CancellationToken);

        Assert.IsNull(result);
        Assert.AreEqual(1, host.RefreshTokens.RotateCalls);
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
            SignInManager<IdentityUser> signInManager,
            ApplicationDbContext context,
            AuthenticationFlowService service,
            Mock<IJwtTokenService> jwtTokens,
            TestRefreshTokenDataAccess refreshTokens,
            Mock<IAuthenticationTimingService> authenticationTiming,
            ITransactionServiceHandle transactionHandle,
            ITransactionService<ApplicationDbContext> transactionService)
        {
            _connection = connection;
            _services = services;
            _scope = scope;
            UserManager = userManager;
            SignInManager = signInManager;
            Context = context;
            Service = service;
            JwtTokens = jwtTokens;
            RefreshTokens = refreshTokens;
            AuthenticationTiming = authenticationTiming;
            TransactionHandle = transactionHandle;
            TransactionService = transactionService;
        }

        public UserManager<IdentityUser> UserManager { get; }

        public SignInManager<IdentityUser> SignInManager { get; }

        public ApplicationDbContext Context { get; }

        public AuthenticationFlowService Service { get; }

        public Mock<IJwtTokenService> JwtTokens { get; }

        public TestRefreshTokenDataAccess RefreshTokens { get; }

        public Mock<IAuthenticationTimingService> AuthenticationTiming { get; }

        public ITransactionServiceHandle TransactionHandle { get; }

        public ITransactionService<ApplicationDbContext> TransactionService { get; }

        public static async Task<TestHost> CreateAsync(CancellationToken cancellationToken)
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync(cancellationToken);

            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton<IModelLoader, SqliteApplicationModelLoader>();
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
            TestRefreshTokenDataAccess refreshTokens = new();
            Mock<IAuthenticationTimingService> authenticationTiming = new();
            UserManager<IdentityUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            SignInManager<IdentityUser> signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<IdentityUser>>();
            ITransactionServiceHandle transactionHandle = scope.ServiceProvider.GetRequiredService<ITransactionServiceHandle>();
            ITransactionService<ApplicationDbContext> transactionService = scope.ServiceProvider.GetRequiredService<ITransactionService<ApplicationDbContext>>();
            AuthenticationFlowService service = new(userManager, signInManager, jwtTokens.Object, refreshTokens, authenticationTiming.Object, transactionHandle);

            return new TestHost(
                connection,
                serviceProvider,
                scope,
                userManager,
                signInManager,
                context,
                service,
                jwtTokens,
                refreshTokens,
                authenticationTiming,
                transactionHandle,
                transactionService);
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

    private sealed class TestRefreshTokenDataAccess : IRefreshTokenDataAccess
    {
        public Func<ApplicationDbContext, string, string?, CancellationToken, Task<RefreshTokenIssueResult>>? IssueHandler { get; set; }

        public Func<ApplicationDbContext, string, CancellationToken, Task<RefreshTokenRotationResult?>>? RotateHandler { get; set; }

        public Func<ApplicationDbContext, string, CancellationToken, Task>? RevokeFamilyHandler { get; set; }

        public Func<ApplicationDbContext, string, CancellationToken, Task>? RevokeUserHandler { get; set; }

        public int IssueCalls { get; private set; }

        public int RotateCalls { get; private set; }

        public int RevokeFamilyCalls { get; private set; }

        public int RevokeUserCalls { get; private set; }

        public Task<RefreshTokenIssueResult> IssueAsync(ApplicationDbContext context, string userId, string? securityStamp, CancellationToken cancellationToken = default)
        {
            IssueCalls++;
            return (IssueHandler ?? throw new AssertFailedException("Unexpected refresh-token issue operation."))(context, userId, securityStamp, cancellationToken);
        }

        public Task<RefreshTokenRotationResult?> RotateAsync(ApplicationDbContext context, string token, CancellationToken cancellationToken = default)
        {
            RotateCalls++;
            return (RotateHandler ?? throw new AssertFailedException("Unexpected refresh-token rotation operation."))(context, token, cancellationToken);
        }

        public Task RevokeFamilyAsync(ApplicationDbContext context, string token, CancellationToken cancellationToken = default)
        {
            RevokeFamilyCalls++;
            return (RevokeFamilyHandler ?? throw new AssertFailedException("Unexpected refresh-token family revocation operation."))(context, token, cancellationToken);
        }

        public Task RevokeUserAsync(ApplicationDbContext context, string userId, CancellationToken cancellationToken = default)
        {
            RevokeUserCalls++;
            return (RevokeUserHandler ?? throw new AssertFailedException("Unexpected refresh-token user revocation operation."))(context, userId, cancellationToken);
        }

        public void AssertNoCalls()
        {
            Assert.AreEqual(0, IssueCalls);
            Assert.AreEqual(0, RotateCalls);
            Assert.AreEqual(0, RevokeFamilyCalls);
            Assert.AreEqual(0, RevokeUserCalls);
        }
    }
}
