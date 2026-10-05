using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Data;
using Ufw.Shared.Firewall;
using Ufw.Shared.Management.KnownHosts;
using Ufw.Web.Data;
using Ufw.Web.Data.Access;
using Ufw.Web.Data.Access.KnownHosts;
using Ufw.Web.Data.Model;
using Ufw.Web.Model.V1.KnownHosts;
using Ufw.Web.Services.KnownHosts;
using Ufw.Web.Tests.Data;
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

        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> result = await host.Service.CreateAsync(
            new CreateKnownHostRequest
            {
                Name = "  Database primary  ",
                Address = "192.0.2.129/24",
                Comment = "  production subnet  ",
                IsVisible = true,
            },
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess);
        KnownHostInventoryItem item = result.Value.Single();
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
        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> first = await host.Service.CreateAsync(
            new CreateKnownHostRequest { Name = "NAS", Address = "192.0.2.10" },
            TestContext.CancellationToken);
        Assert.IsTrue(first.IsSuccess);

        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> duplicate = await host.Service.CreateAsync(
            new CreateKnownHostRequest { Name = "nas", Address = "192.0.2.11" },
            TestContext.CancellationToken);

        Assert.IsInstanceOfType<DataMutationUniqueConflictError>(duplicate.Error);
        IReadOnlyList<KnownHostInventoryItem> inventory = await host.Service.GetAsync(TestContext.CancellationToken);
        Assert.HasCount(1, inventory);
        Assert.AreEqual("192.0.2.10", inventory[0].Address);
    }

    [TestMethod]
    public async Task UpdateAsync_SameFamilyAddressChange_UpdatesAliasWithoutChangingPublicIdAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> created = await host.Service.CreateAsync(
            new CreateKnownHostRequest { Name = "proxy", Address = "192.0.2.10" },
            TestContext.CancellationToken);
        KnownHostInventoryItem existing = created.Value.Single();

        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> updated = await host.Service.UpdateAsync(
            existing.Id,
            new UpdateKnownHostRequest
            {
                Name = "edge proxy",
                Address = "198.51.100.44",
                Comment = "frontend",
                IsVisible = false,
            },
            TestContext.CancellationToken);

        Assert.IsTrue(updated.IsSuccess);
        KnownHostInventoryItem item = updated.Value.Single();
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
        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> created = await host.Service.CreateAsync(
            new CreateKnownHostRequest { Name = "router", Address = "192.0.2.1" },
            TestContext.CancellationToken);
        KnownHostInventoryItem existing = created.Value.Single();

        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> updated = await host.Service.UpdateAsync(
            existing.Id,
            new UpdateKnownHostRequest { Name = "router", Address = "2001:db8::1" },
            TestContext.CancellationToken);

        Assert.IsInstanceOfType<KnownHostAddressFamilyConflictError>(updated.Error);
        KnownHostInventoryItem persisted = (await host.Service.GetAsync(TestContext.CancellationToken)).Single();
        Assert.AreEqual("192.0.2.1", persisted.Address);
        Assert.AreEqual(FirewallAddressFamily.IPv4, persisted.AddressFamily);
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
    public async Task CreateAsync_DnsBackedAlias_ResolvesAndPersistsDnsMetadataAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.DnsResolver.Result = "192.0.2.44";

        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> result = await host.Service.CreateAsync(
            new CreateKnownHostRequest
            {
                Name = "db.example.test",
                AddressSource = KnownHostAddressSource.Dns,
                DnsAddressFamily = FirewallAddressFamily.IPv4,
            },
            TestContext.CancellationToken);

        Assert.IsTrue(result.IsSuccess);
        KnownHostInventoryItem item = result.Value.Single();
        Assert.AreEqual("192.0.2.44", item.Address);
        Assert.AreEqual(KnownHostAddressSource.Dns, item.AddressSource);
        Assert.AreEqual(host.TimeProvider.GetUtcNow(), item.DnsResolvedAt);
        Assert.AreEqual(1, host.DnsResolver.CallCount);
        Assert.AreEqual("db.example.test", host.DnsResolver.LastName);
        Assert.AreEqual(FirewallAddressFamily.IPv4, host.DnsResolver.LastFamily);
    }

    [TestMethod]
    public async Task CreateAsync_DnsResolutionFailure_DoesNotPersistAliasAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.DnsResolver.Result = null;

        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> result = await host.Service.CreateAsync(
            new CreateKnownHostRequest
            {
                Name = "missing.example.test",
                AddressSource = KnownHostAddressSource.Dns,
                DnsAddressFamily = FirewallAddressFamily.IPv4,
            },
            TestContext.CancellationToken);

        Assert.IsInstanceOfType<KnownHostDnsResolutionFailedError>(result.Error);
        Assert.IsEmpty((await host.Service.GetAsync(TestContext.CancellationToken)));
    }

    [TestMethod]
    public async Task UpdateAsync_UnchangedDnsConfiguration_DoesNotResolveAgainAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.DnsResolver.Result = "192.0.2.10";
        KnownHostInventoryItem created = (await host.Service.CreateAsync(
            new CreateKnownHostRequest
            {
                Name = "nas.example.test",
                AddressSource = KnownHostAddressSource.Dns,
                DnsAddressFamily = FirewallAddressFamily.IPv4,
            },
            TestContext.CancellationToken)).Value.Single();
        int callsAfterCreate = host.DnsResolver.CallCount;

        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> updated = await host.Service.UpdateAsync(
            created.Id,
            new UpdateKnownHostRequest
            {
                Name = created.Name,
                AddressSource = KnownHostAddressSource.Dns,
                DnsAddressFamily = created.AddressFamily,
                Comment = "storage",
                IsVisible = false,
            },
            TestContext.CancellationToken);

        Assert.IsTrue(updated.IsSuccess);
        KnownHostInventoryItem item = updated.Value.Single();
        Assert.AreEqual(callsAfterCreate, host.DnsResolver.CallCount);
        Assert.AreEqual(created.Address, item.Address);
        Assert.AreEqual(created.DnsResolvedAt, item.DnsResolvedAt);
        Assert.AreEqual("storage", item.Comment);
        Assert.IsFalse(item.IsVisible);
    }

    [TestMethod]
    public async Task UpdateAsync_ChangedDnsName_ResolvesAgainWithinExistingFamilyAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.DnsResolver.Result = "192.0.2.10";
        KnownHostInventoryItem created = (await host.Service.CreateAsync(
            new CreateKnownHostRequest
            {
                Name = "nas.example.test",
                AddressSource = KnownHostAddressSource.Dns,
                DnsAddressFamily = FirewallAddressFamily.IPv4,
            },
            TestContext.CancellationToken)).Value.Single();
        DateTimeOffset createdAt = created.DnsResolvedAt!.Value;
        host.TimeProvider.Advance(TimeSpan.FromMinutes(30));
        host.DnsResolver.Result = "192.0.2.20";

        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> updated = await host.Service.UpdateAsync(
            created.Id,
            new UpdateKnownHostRequest
            {
                Name = "storage.example.test",
                AddressSource = KnownHostAddressSource.Dns,
                DnsAddressFamily = FirewallAddressFamily.IPv4,
                IsVisible = true,
            },
            TestContext.CancellationToken);

        Assert.IsTrue(updated.IsSuccess);
        KnownHostInventoryItem item = updated.Value.Single();
        Assert.AreEqual(created.Id, item.Id);
        Assert.AreEqual("storage.example.test", item.Name);
        Assert.AreEqual("192.0.2.20", item.Address);
        Assert.AreNotEqual(createdAt, item.DnsResolvedAt);
        Assert.AreEqual(host.TimeProvider.GetUtcNow(), item.DnsResolvedAt);
        Assert.AreEqual("storage.example.test", host.DnsResolver.LastName);
        Assert.AreEqual(FirewallAddressFamily.IPv4, host.DnsResolver.LastFamily);
    }

    [TestMethod]
    public async Task ReconcileDnsAsync_RefreshesAddressAndTimestampWithoutChangingAliasIdentityAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.DnsResolver.Result = "192.0.2.10";
        KnownHostInventoryItem created = (await host.Service.CreateAsync(
            new CreateKnownHostRequest
            {
                Name = "edge.example.test",
                AddressSource = KnownHostAddressSource.Dns,
                DnsAddressFamily = FirewallAddressFamily.IPv4,
            },
            TestContext.CancellationToken)).Value.Single();
        host.TimeProvider.Advance(TimeSpan.FromHours(2));
        host.DnsResolver.Result = "192.0.2.20";

        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> reconciled = await host.Service.ReconcileDnsAsync(created.Id, TestContext.CancellationToken);

        Assert.IsTrue(reconciled.IsSuccess);
        KnownHostInventoryItem item = reconciled.Value.Single();
        Assert.AreEqual(created.Id, item.Id);
        Assert.AreEqual("192.0.2.20", item.Address);
        Assert.AreEqual(host.TimeProvider.GetUtcNow(), item.DnsResolvedAt);
        Assert.AreEqual(created.Address, host.DnsResolver.LastCurrentAddress);
    }

    [TestMethod]
    public async Task ReconcileDnsAsync_LiteralAlias_IsRejectedAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        KnownHostInventoryItem created = (await host.Service.CreateAsync(
            new CreateKnownHostRequest { Name = "router", Address = "192.0.2.1" },
            TestContext.CancellationToken)).Value.Single();

        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> result = await host.Service.ReconcileDnsAsync(created.Id, TestContext.CancellationToken);

        Assert.IsInstanceOfType<KnownHostNotDnsManagedError>(result.Error);
        Assert.AreEqual(0, host.DnsResolver.CallCount);
    }

    [TestMethod]
    public async Task DeleteAsync_RemovesOnlyRequestedAliasAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> created = await host.Service.CreateAsync(
            new CreateKnownHostRequest { Name = "one", Address = "192.0.2.1" },
            TestContext.CancellationToken);
        KnownHostInventoryItem first = created.Value.Single();
        _ = await host.Service.CreateAsync(
            new CreateKnownHostRequest { Name = "two", Address = "192.0.2.2" },
            TestContext.CancellationToken);

        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> deleted = await host.Service.DeleteAsync(first.Id, TestContext.CancellationToken);

        Assert.IsTrue(deleted.IsSuccess);
        KnownHostInventoryItem remaining = deleted.Value.Single();
        Assert.AreEqual("two", remaining.Name);
        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> missing = await host.Service.DeleteAsync(first.Id, TestContext.CancellationToken);
        Assert.IsInstanceOfType<DataMutationNotFoundError>(missing.Error);
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _services;

        private TestHost(
            SqliteConnection connection,
            ServiceProvider services,
            AsyncServiceScope scope,
            KnownHostService service,
            TestKnownHostDnsResolver dnsResolver,
            TestTimeProvider timeProvider)
        {
            _connection = connection;
            _services = services;
            Scope = scope;
            Service = service;
            DnsResolver = dnsResolver;
            TimeProvider = timeProvider;
        }

        public AsyncServiceScope Scope { get; }

        public KnownHostService Service { get; }

        public TestKnownHostDnsResolver DnsResolver { get; }

        public TestTimeProvider TimeProvider { get; }

        public static async Task<TestHost> CreateAsync(CancellationToken cancellationToken)
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync(cancellationToken);

            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton<IModelLoader, SqliteApplicationModelLoader>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddTransactionManagement<ApplicationDbContext>(options => options.UseIsolationLevel(IsolationLevel.ReadCommitted));

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);

            ITransactionServiceHandle transactionHandle = scope.ServiceProvider.GetRequiredService<ITransactionServiceHandle>();
            KnownHostDataAccess dataAccess = new(transactionHandle);
            TestKnownHostDnsResolver dnsResolver = new();
            TestTimeProvider timeProvider = new(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero));
            KnownHostService service = new(dataAccess, dnsResolver, timeProvider);
            return new TestHost(connection, serviceProvider, scope, service, dnsResolver, timeProvider);
        }

        internal sealed class TestKnownHostDnsResolver : IKnownHostDnsResolver
        {
            public string? Result { get; set; }
            public int CallCount { get; private set; }
            public string? LastName { get; private set; }
            public FirewallAddressFamily LastFamily { get; private set; }
            public string? LastCurrentAddress { get; private set; }

            public Task<string?> ResolveAsync(string dnsName, FirewallAddressFamily addressFamily, string? currentAddress = null, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                LastName = dnsName;
                LastFamily = addressFamily;
                LastCurrentAddress = currentAddress;
                return Task.FromResult(Result);
            }
        }

        internal sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
        {
            private DateTimeOffset _utcNow = utcNow;

            public override DateTimeOffset GetUtcNow() => _utcNow;

            public void Advance(TimeSpan duration) => _utcNow += duration;
        }

        public async ValueTask DisposeAsync()
        {
            await Scope.DisposeAsync();
            await _services.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
