using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using Ufw.Web.Data;
using Wkg.AspNetCore.Transactions;
using Wkg.AspNetCore.Transactions.Configuration;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web.Tests.Data;

[TestClass]
public sealed class TransactionIntegrationTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ScopedTransaction_RollbackIncludesIdentityWritesAsync()
    {
        await using TransactionTestHost host = await TransactionTestHost.CreateAsync(TestContext.CancellationToken);

        await host.CreateUserAsync("rollback@example.invalid", commit: false, TestContext.CancellationToken);

        Assert.IsNull(await host.FindUserAsync("rollback@example.invalid", TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ScopedTransaction_CommitIncludesIdentityWritesAsync()
    {
        await using TransactionTestHost host = await TransactionTestHost.CreateAsync(TestContext.CancellationToken);

        await host.CreateUserAsync("commit@example.invalid", commit: true, TestContext.CancellationToken);

        Assert.IsNotNull(await host.FindUserAsync("commit@example.invalid", TestContext.CancellationToken));
    }

    private sealed class TransactionTestHost : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _serviceProvider;

        private TransactionTestHost(SqliteConnection connection, ServiceProvider serviceProvider)
        {
            _connection = connection;
            _serviceProvider = serviceProvider;
        }

        public static async Task<TransactionTestHost> CreateAsync(CancellationToken cancellationToken)
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync(cancellationToken);

            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton<IModelLoader, ApplicationModelLoader>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<IdentityUser>(options =>
                {
                    options.Password.RequiredLength = 5;
                    options.Password.RequireDigit = false;
                    options.Password.RequireLowercase = false;
                    options.Password.RequireUppercase = false;
                    options.Password.RequireNonAlphanumeric = false;
                })
                .AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddTransactionManagement<ApplicationDbContext>(options =>
                options.UseIsolationLevel(IsolationLevel.Serializable));

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);

            return new TransactionTestHost(connection, serviceProvider);
        }

        public async Task CreateUserAsync(string email, bool commit, CancellationToken cancellationToken)
        {
            await using AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
            UserManager<IdentityUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            ITransactionService<ApplicationDbContext> transactionService =
                scope.ServiceProvider.GetRequiredService<ITransactionService<ApplicationDbContext>>();

            await transactionService.Scoped.RunAsync(async (_, transaction, ct) =>
            {
                IdentityResult result = await userManager.CreateAsync(new IdentityUser
                {
                    UserName = email,
                    Email = email,
                }, "admin");
                Assert.IsTrue(result.Succeeded, string.Join("; ", result.Errors.Select(static error => error.Description)));
                return commit ? transaction.Commit() : transaction.Rollback();
            }, cancellationToken);
        }

        public async Task<IdentityUser?> FindUserAsync(string email, CancellationToken cancellationToken)
        {
            await using AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
            UserManager<IdentityUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            cancellationToken.ThrowIfCancellationRequested();
            return await userManager.FindByEmailAsync(email);
        }

        public async ValueTask DisposeAsync()
        {
            await _serviceProvider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
