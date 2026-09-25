using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Data;
using Ufw.Web.Configuration;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Ufw.Web.Services.Auth;
using Ufw.Web.Tests.Data;
using Wkg.AspNetCore.Transactions;
using Wkg.AspNetCore.Transactions.Configuration;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web.Tests.Services.Auth;

[TestClass]
public sealed class RefreshTokenServiceTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task RotateAsync_ReusedToken_RevokesActiveFamilyAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        IdentityUser user = CreateUser("test-user");
        host.Context.Users.Add(user);
        await host.Context.SaveChangesAsync(TestContext.CancellationToken);

        RefreshTokenIssueResult issued = await host.Service.IssueAsync(user, TestContext.CancellationToken);
        RefreshToken persistedToken = await host.Context.Set<RefreshToken>().SingleAsync(TestContext.CancellationToken);
        Assert.AreNotEqual(issued.Token, persistedToken.TokenHash);
        Assert.AreEqual(64, persistedToken.TokenHash.Length);

        RefreshTokenRotationResult? rotation = await host.Service.RotateAsync(issued.Token, TestContext.CancellationToken);
        Assert.IsNotNull(rotation);
        Assert.AreNotEqual(issued.Token, rotation.Token);

        RefreshTokenRotationResult? replay = await host.Service.RotateAsync(issued.Token, TestContext.CancellationToken);
        Assert.IsNull(replay);

        host.Context.ChangeTracker.Clear();
        int activeFamilyTokenCount = await host.Context.Set<RefreshToken>()
            .CountAsync(token => token.FamilyId == persistedToken.FamilyId && token.RevokedAt == null, TestContext.CancellationToken);
        Assert.AreEqual(0, activeFamilyTokenCount);
    }

    [TestMethod]
    public async Task IssueAsync_OuterRollback_RollsBackNestedCommitAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        IdentityUser user = CreateUser("test-user");
        host.Context.Users.Add(user);
        await host.Context.SaveChangesAsync(TestContext.CancellationToken);

        await host.TransactionService.Scoped.RunAsync(async (_, transaction, ct) =>
        {
            await host.Service.IssueAsync(user, ct);
            return transaction.Rollback();
        }, TestContext.CancellationToken);

        await host.TransactionService.Scoped.DisposeAsync();
        host.Context.ChangeTracker.Clear();
        Assert.AreEqual(0, await host.Context.Set<RefreshToken>().CountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task RevokeFamilyAsync_PreviousFamilyToken_RevokesReplacementAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        IdentityUser user = CreateUser("test-user");
        host.Context.Users.Add(user);
        await host.Context.SaveChangesAsync(TestContext.CancellationToken);

        RefreshTokenIssueResult issued = await host.Service.IssueAsync(user, TestContext.CancellationToken);
        RefreshTokenRotationResult? rotation = await host.Service.RotateAsync(issued.Token, TestContext.CancellationToken);
        Assert.IsNotNull(rotation);

        await host.Service.RevokeFamilyAsync(issued.Token, TestContext.CancellationToken);

        host.Context.ChangeTracker.Clear();
        Assert.AreEqual(
            0,
            await host.Context.Set<RefreshToken>().CountAsync(
                token => token.RevokedAt == null,
                TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task RevokeUserAsync_RevokesOnlyTargetUsersActiveTokensAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        IdentityUser target = CreateUser("target");
        IdentityUser other = CreateUser("other");
        host.Context.Users.AddRange(target, other);
        await host.Context.SaveChangesAsync(TestContext.CancellationToken);

        _ = await host.Service.IssueAsync(target, TestContext.CancellationToken);
        _ = await host.Service.IssueAsync(other, TestContext.CancellationToken);
        await host.Service.RevokeUserAsync(target.Id, TestContext.CancellationToken);

        host.Context.ChangeTracker.Clear();
        Assert.AreEqual(0, await host.Context.Set<RefreshToken>().CountAsync(token => token.UserId == target.Id && token.RevokedAt == null, TestContext.CancellationToken));
        Assert.AreEqual(1, await host.Context.Set<RefreshToken>().CountAsync(token => token.UserId == other.Id && token.RevokedAt == null, TestContext.CancellationToken));
    }

    private static IdentityUser CreateUser(string name) => new()
    {
        Id = Guid.NewGuid().ToString(),
        UserName = name,
        NormalizedUserName = name.ToUpperInvariant(),
        Email = $"{name}@example.invalid",
        NormalizedEmail = $"{name.ToUpperInvariant()}@EXAMPLE.INVALID",
        SecurityStamp = Guid.NewGuid().ToString(),
    };

    private sealed class TestHost : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _serviceProvider;
        private readonly AsyncServiceScope _scope;

        private TestHost(
            SqliteConnection connection,
            ServiceProvider serviceProvider,
            AsyncServiceScope scope,
            ApplicationDbContext context,
            RefreshTokenService service,
            ITransactionService<ApplicationDbContext> transactionService)
        {
            _connection = connection;
            _serviceProvider = serviceProvider;
            _scope = scope;
            Context = context;
            Service = service;
            TransactionService = transactionService;
        }

        public ApplicationDbContext Context { get; }

        public RefreshTokenService Service { get; }

        public ITransactionService<ApplicationDbContext> TransactionService { get; }

        public static async Task<TestHost> CreateAsync(CancellationToken cancellationToken)
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync(cancellationToken);

            ServiceCollection services = new();
            services.AddSingleton<IModelLoader, SqliteApplicationModelLoader>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddTransactionManagement<ApplicationDbContext>(options => options.UseIsolationLevel(IsolationLevel.ReadCommitted));

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);
            ITransactionServiceHandle transactionHandle = scope.ServiceProvider.GetRequiredService<ITransactionServiceHandle>();
            ITransactionService<ApplicationDbContext> transactionService = scope.ServiceProvider.GetRequiredService<ITransactionService<ApplicationDbContext>>();
            RefreshTokenService service = new(transactionHandle, Options.Create(new RefreshTokenOptions { Lifetime = TimeSpan.FromDays(1) }), TimeProvider.System);
            return new TestHost(connection, serviceProvider, scope, context, service, transactionService);
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _serviceProvider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
