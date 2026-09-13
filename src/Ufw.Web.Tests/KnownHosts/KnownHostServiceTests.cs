using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using Ufw.Shared.Firewall;
using Ufw.Web.Api.V1.Models.KnownHosts;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Ufw.Web.Services.KnownHosts;
using Wkg.AspNetCore.Exceptions;
using Wkg.AspNetCore.Transactions;
using Wkg.AspNetCore.Transactions.Configuration;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web.Tests.KnownHosts;

[TestClass]
public sealed class KnownHostServiceTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task CreateAsync_NormalizesMetadataAndPersistsUuidV7Async()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);

        KnownHostMutationResult result = await host.Service.CreateAsync(
            new CreateKnownHostRequest
            {
                Name = "  Database primary  ",
                Address = "192.0.2.129/24",
                Comment = "  production subnet  ",
                IsVisible = true,
            },
            TestContext.CancellationToken);

        Assert.AreEqual(KnownHostMutationOutcome.Success, result.Outcome);
        Assert.IsNotNull(result.Inventory);
        KnownHostItem item = result.Inventory.Hosts.Single();
        Assert.AreEqual('7', item.Id.ToString("D")[14]);
        Assert.AreEqual("Database primary", item.Name);
        Assert.AreEqual("192.0.2.0/24", item.Address);
        Assert.AreEqual(FirewallAddressFamily.IPv4, item.AddressFamily);
        Assert.AreEqual("production subnet", item.Comment);
        Assert.IsTrue(item.IsVisible);

        ApplicationDbContext context = host.Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.ChangeTracker.Clear();
        KnownHostEntry persisted = await context.Set<KnownHostEntry>().SingleAsync(TestContext.CancellationToken);
        Assert.AreEqual("DATABASE PRIMARY", persisted.NormalizedName);
        Assert.AreEqual(item.Id, persisted.PublicId);
    }

    [TestMethod]
    public async Task CreateAsync_CaseInsensitiveDuplicateName_ReturnsConflictWithoutAddingRowAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        KnownHostMutationResult first = await host.Service.CreateAsync(
            new CreateKnownHostRequest { Name = "NAS", Address = "192.0.2.10" },
            TestContext.CancellationToken);
        Assert.AreEqual(KnownHostMutationOutcome.Success, first.Outcome);

        KnownHostMutationResult duplicate = await host.Service.CreateAsync(
            new CreateKnownHostRequest { Name = "nas", Address = "192.0.2.11" },
            TestContext.CancellationToken);

        Assert.AreEqual(KnownHostMutationOutcome.NameConflict, duplicate.Outcome);
        KnownHostInventoryResponse inventory = await host.Service.GetAsync(TestContext.CancellationToken);
        Assert.HasCount(1, inventory.Hosts);
        Assert.AreEqual("192.0.2.10", inventory.Hosts[0].Address);
    }

    [TestMethod]
    public async Task UpdateAsync_SameFamilyAddressChange_UpdatesAliasWithoutChangingPublicIdAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        KnownHostMutationResult created = await host.Service.CreateAsync(
            new CreateKnownHostRequest { Name = "proxy", Address = "192.0.2.10" },
            TestContext.CancellationToken);
        Assert.IsNotNull(created.Inventory);
        KnownHostItem existing = created.Inventory.Hosts.Single();

        KnownHostMutationResult updated = await host.Service.UpdateAsync(
            existing.Id,
            new UpdateKnownHostRequest
            {
                Name = "edge proxy",
                Address = "198.51.100.44",
                Comment = "frontend",
                IsVisible = false,
            },
            TestContext.CancellationToken);

        Assert.AreEqual(KnownHostMutationOutcome.Success, updated.Outcome);
        Assert.IsNotNull(updated.Inventory);
        KnownHostItem item = updated.Inventory.Hosts.Single();
        Assert.AreEqual(existing.Id, item.Id);
        Assert.AreEqual("edge proxy", item.Name);
        Assert.AreEqual("198.51.100.44", item.Address);
        Assert.AreEqual("frontend", item.Comment);
        Assert.IsFalse(item.IsVisible);
    }

    [TestMethod]
    public async Task UpdateAsync_AddressFamilyChange_IsRejectedAndLeavesAliasUnchangedAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        KnownHostMutationResult created = await host.Service.CreateAsync(
            new CreateKnownHostRequest { Name = "router", Address = "192.0.2.1" },
            TestContext.CancellationToken);
        Assert.IsNotNull(created.Inventory);
        KnownHostItem existing = created.Inventory.Hosts.Single();

        KnownHostMutationResult updated = await host.Service.UpdateAsync(
            existing.Id,
            new UpdateKnownHostRequest { Name = "router", Address = "2001:db8::1" },
            TestContext.CancellationToken);

        Assert.AreEqual(KnownHostMutationOutcome.AddressFamilyConflict, updated.Outcome);
        KnownHostItem persisted = (await host.Service.GetAsync(TestContext.CancellationToken)).Hosts.Single();
        Assert.AreEqual("192.0.2.1", persisted.Address);
        Assert.AreEqual(FirewallAddressFamily.IPv4, persisted.AddressFamily);
    }

    [TestMethod]
    public async Task CreateAsync_InvalidAddress_IsRejectedBeforePersistenceAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);

        KnownHostMutationResult result = await host.Service.CreateAsync(
            new CreateKnownHostRequest { Name = "broken", Address = "192.0.2.1/99" },
            TestContext.CancellationToken);

        Assert.AreEqual(KnownHostMutationOutcome.InvalidAddress, result.Outcome);
        Assert.IsEmpty((await host.Service.GetAsync(TestContext.CancellationToken)).Hosts);
    }

    [TestMethod]
    public async Task GetAsync_InvalidPersistedAddress_FailsClosedAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        ApplicationDbContext context = host.Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Add(new KnownHostEntry
        {
            Name = "broken",
            NormalizedName = "BROKEN",
            Address = "not-an-address",
        });
        await context.SaveChangesAsync(TestContext.CancellationToken);

        ApiProxyException exception = await Assert.ThrowsExactlyAsync<ApiProxyException>(
            () => host.Service.GetAsync(TestContext.CancellationToken));
        Assert.IsInstanceOfType<InvalidDataException>(exception.InnerException);
    }

    [TestMethod]
    public async Task DeleteAsync_RemovesOnlyRequestedAliasAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        KnownHostMutationResult created = await host.Service.CreateAsync(
            new CreateKnownHostRequest { Name = "one", Address = "192.0.2.1" },
            TestContext.CancellationToken);
        Assert.IsNotNull(created.Inventory);
        KnownHostItem first = created.Inventory.Hosts.Single();
        _ = await host.Service.CreateAsync(
            new CreateKnownHostRequest { Name = "two", Address = "192.0.2.2" },
            TestContext.CancellationToken);

        KnownHostMutationResult deleted = await host.Service.DeleteAsync(first.Id, TestContext.CancellationToken);

        Assert.AreEqual(KnownHostMutationOutcome.Success, deleted.Outcome);
        Assert.IsNotNull(deleted.Inventory);
        KnownHostItem remaining = deleted.Inventory.Hosts.Single();
        Assert.AreEqual("two", remaining.Name);
        Assert.AreEqual(KnownHostMutationOutcome.NotFound, (await host.Service.DeleteAsync(first.Id, TestContext.CancellationToken)).Outcome);
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _services;

        private TestHost(SqliteConnection connection, ServiceProvider services, AsyncServiceScope scope, KnownHostService service)
        {
            _connection = connection;
            _services = services;
            Scope = scope;
            Service = service;
        }

        public AsyncServiceScope Scope { get; }

        public KnownHostService Service { get; }

        public static async Task<TestHost> CreateAsync(CancellationToken cancellationToken)
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync(cancellationToken);

            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton<IModelLoader, ApplicationModelLoader>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddTransactionManagement<ApplicationDbContext>(options => options.UseIsolationLevel(IsolationLevel.ReadCommitted));

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);

            ITransactionServiceHandle transactionHandle = scope.ServiceProvider.GetRequiredService<ITransactionServiceHandle>();
            KnownHostRepository repository = new(transactionHandle);
            KnownHostService service = new(repository);
            return new TestHost(connection, serviceProvider, scope, service);
        }

        public async ValueTask DisposeAsync()
        {
            await Scope.DisposeAsync();
            await _services.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
