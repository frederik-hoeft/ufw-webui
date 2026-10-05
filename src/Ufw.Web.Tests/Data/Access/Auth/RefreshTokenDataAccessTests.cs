using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Data;
using Ufw.Web.Configuration;
using Ufw.Web.Data;
using Ufw.Web.Data.Access.Auth;
using Ufw.Web.Data.Model;
using Ufw.Web.Tests.Data;
using Wkg.AspNetCore.Transactions;
using Wkg.AspNetCore.Transactions.Configuration;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web.Tests.Data.Access.Auth;

[TestClass]
public sealed class RefreshTokenDataAccessTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task RotateAsync_ReusedToken_RevokesActiveFamilyAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        IdentityUser user = await host.CreateUserAsync("test-user", TestContext.CancellationToken);

        RefreshTokenIssueResult issued = await host.CommitAsync(
            (context, ct) => host.DataAccess.IssueAsync(context, user.Id, user.SecurityStamp, ct),
            TestContext.CancellationToken);
        RefreshToken persistedToken = await host.Context.Set<RefreshToken>().SingleAsync(TestContext.CancellationToken);
        Assert.AreNotEqual(issued.Token, persistedToken.TokenHash);
        Assert.AreEqual(64, persistedToken.TokenHash.Length);

        RefreshTokenRotationResult? rotation = await host.CommitAsync(
            (context, ct) => host.DataAccess.RotateAsync(context, issued.Token, ct),
            TestContext.CancellationToken);
        Assert.IsNotNull(rotation);
        Assert.AreEqual(user.Id, rotation.UserId);
        Assert.AreNotEqual(issued.Token, rotation.Token);

        RefreshTokenRotationResult? replay = await host.CommitAsync(
            (context, ct) => host.DataAccess.RotateAsync(context, issued.Token, ct),
            TestContext.CancellationToken);
        Assert.IsNull(replay);

        host.Context.ChangeTracker.Clear();
        int activeFamilyTokenCount = await host.Context.Set<RefreshToken>()
            .CountAsync(token => token.FamilyId == persistedToken.FamilyId && token.RevokedAt == null, TestContext.CancellationToken);
        Assert.AreEqual(0, activeFamilyTokenCount);
    }

    [TestMethod]
    public async Task IssueAsync_CallerRollback_RollsBackPersistenceAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        IdentityUser user = await host.CreateUserAsync("test-user", TestContext.CancellationToken);

        await host.TransactionService.Scoped.RunAsync(async (context, transaction, ct) =>
        {
            _ = await host.DataAccess.IssueAsync(context, user.Id, user.SecurityStamp, ct);
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
        IdentityUser user = await host.CreateUserAsync("test-user", TestContext.CancellationToken);

        RefreshTokenIssueResult issued = await host.CommitAsync(
            (context, ct) => host.DataAccess.IssueAsync(context, user.Id, user.SecurityStamp, ct),
            TestContext.CancellationToken);
        RefreshTokenRotationResult? rotation = await host.CommitAsync(
            (context, ct) => host.DataAccess.RotateAsync(context, issued.Token, ct),
            TestContext.CancellationToken);
        Assert.IsNotNull(rotation);

        await host.CommitAsync(
            (context, ct) => host.DataAccess.RevokeFamilyAsync(context, issued.Token, ct),
            TestContext.CancellationToken);

        host.Context.ChangeTracker.Clear();
        Assert.AreEqual(
            0,
            await host.Context.Set<RefreshToken>().CountAsync(token => token.RevokedAt == null, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task RevokeUserAsync_RevokesOnlyTargetUsersActiveTokensAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        IdentityUser target = await host.CreateUserAsync("target", TestContext.CancellationToken);
        IdentityUser other = await host.CreateUserAsync("other", TestContext.CancellationToken);

        _ = await host.CommitAsync(
            (context, ct) => host.DataAccess.IssueAsync(context, target.Id, target.SecurityStamp, ct),
            TestContext.CancellationToken);
        _ = await host.CommitAsync(
            (context, ct) => host.DataAccess.IssueAsync(context, other.Id, other.SecurityStamp, ct),
            TestContext.CancellationToken);
        await host.CommitAsync(
            (context, ct) => host.DataAccess.RevokeUserAsync(context, target.Id, ct),
            TestContext.CancellationToken);

        host.Context.ChangeTracker.Clear();
        Assert.AreEqual(0, await host.Context.Set<RefreshToken>().CountAsync(token => token.UserId == target.Id && token.RevokedAt == null, TestContext.CancellationToken));
        Assert.AreEqual(1, await host.Context.Set<RefreshToken>().CountAsync(token => token.UserId == other.Id && token.RevokedAt == null, TestContext.CancellationToken));
    }

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
            RefreshTokenDataAccess dataAccess,
            ITransactionService<ApplicationDbContext> transactionService)
        {
            _connection = connection;
            _serviceProvider = serviceProvider;
            _scope = scope;
            Context = context;
            DataAccess = dataAccess;
            TransactionService = transactionService;
        }

        public ApplicationDbContext Context { get; }

        public RefreshTokenDataAccess DataAccess { get; }

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
            ITransactionService<ApplicationDbContext> transactionService = scope.ServiceProvider.GetRequiredService<ITransactionService<ApplicationDbContext>>();
            RefreshTokenDataAccess dataAccess = new(Options.Create(new RefreshTokenOptions { Lifetime = TimeSpan.FromDays(1) }), TimeProvider.System);
            return new TestHost(connection, serviceProvider, scope, context, dataAccess, transactionService);
        }

        public async Task<IdentityUser> CreateUserAsync(string name, CancellationToken cancellationToken)
        {
            IdentityUser user = new()
            {
                Id = Guid.NewGuid().ToString(),
                UserName = name,
                NormalizedUserName = name.ToUpperInvariant(),
                Email = $"{name}@example.invalid",
                NormalizedEmail = $"{name.ToUpperInvariant()}@EXAMPLE.INVALID",
                SecurityStamp = Guid.NewGuid().ToString(),
            };
            Context.Users.Add(user);
            await Context.SaveChangesAsync(cancellationToken);
            return user;
        }

        public Task<T> CommitAsync<T>(Func<ApplicationDbContext, CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
            TransactionService.Scoped.RunAsync(async (context, transaction, ct) =>
            {
                T result = await operation(context, ct);
                return transaction.Commit(result);
            }, cancellationToken);

        public async Task CommitAsync(Func<ApplicationDbContext, CancellationToken, Task> operation, CancellationToken cancellationToken)
        {
            _ = await TransactionService.Scoped.RunAsync(async (context, transaction, ct) =>
            {
                await operation(context, ct);
                return transaction.Commit(true);
            }, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _serviceProvider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
