using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Data;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Management.NetworkInterfaces;
using Ufw.Web.Data;
using Ufw.Web.Data.Access.NetworkInterfaces;
using Ufw.Web.Data.Model;
using Ufw.Web.Services.Daemon;
using Ufw.Web.Services.NetworkInterfaces;
using Ufw.Web.Tests.Data;
using Wkg.AspNetCore.Transactions;
using Wkg.AspNetCore.Transactions.Configuration;
using Wkg.AspNetCore.Transactions.Continuations;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web.Tests.NetworkInterfaces;

[TestClass]
public sealed class NetworkInterfaceInventoryServiceTests
{
    private static readonly string[] s_vlanInterfaceNames = ["eno1", "enp4s0f2.1100"];

    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ReconcileAsync_PreservesMetadataForPresentInterfacesAndRetainsMissingEntriesAsStaleAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetDaemonInterfaces("docker0", "eno1");

        NetworkInterfaceInventorySnapshot initial = await host.Service.ReconcileAsync(TestContext.CancellationToken);
        NetworkInterfaceInventoryItem docker0 = initial.Interfaces.Single(static item => item.Name == "docker0");
        NetworkInterfaceInventoryItem eno1 = initial.Interfaces.Single(static item => item.Name == "eno1");
        Assert.AreEqual('7', eno1.Id.ToString("D")[14]);
        Assert.IsTrue(eno1.IsVisible);

        NetworkInterfaceInventorySnapshot? commented = await host.Service.UpdateCommentAsync(eno1.Id, " service VLAN ", TestContext.CancellationToken);
        Assert.IsNotNull(commented);
        Assert.AreEqual("service VLAN", commented.Interfaces.Single(static item => item.Name == "eno1").Comment);

        NetworkInterfaceInventorySnapshot? hidden = await host.Service.UpdateVisibilityAsync(eno1.Id, isVisible: false, TestContext.CancellationToken);
        Assert.IsNotNull(hidden);
        Assert.IsFalse(hidden.Interfaces.Single(static item => item.Name == "eno1").IsVisible);

        host.Clock.Advance(TimeSpan.FromMinutes(1));
        host.SetDaemonInterfaces("eno1", "wlan0");
        NetworkInterfaceInventorySnapshot reconciled = await host.Service.ReconcileAsync(TestContext.CancellationToken);
        NetworkInterfaceCleanupResult stale = await host.Service.GetStaleAsync(TestContext.CancellationToken);

        Assert.HasCount(2, reconciled.Interfaces);
        NetworkInterfaceInventoryItem retained = reconciled.Interfaces.Single(static item => item.Name == "eno1");
        Assert.AreEqual(eno1.Id, retained.Id);
        Assert.AreEqual("service VLAN", retained.Comment);
        Assert.IsFalse(retained.IsVisible);
        Assert.IsFalse(reconciled.Interfaces.Any(static item => item.Name == "docker0"));
        NetworkInterfaceInventoryItem wlan0 = reconciled.Interfaces.Single(static item => item.Name == "wlan0");
        Assert.AreEqual('7', wlan0.Id.ToString("D")[14]);
        Assert.IsTrue(wlan0.IsVisible);
        Assert.AreEqual(host.Clock.GetUtcNow(), reconciled.ReconciledAt);
        Assert.HasCount(1, stale.StaleInterfaces);
        Assert.AreEqual(docker0.Id, stale.StaleInterfaces[0].Id);
        Assert.AreEqual("docker0", stale.StaleInterfaces[0].Name);
    }

    [TestMethod]
    public async Task ReconcileAsync_ReappearingInterfaceRestoresSameIdentityAndMetadataAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetDaemonInterfaces("eno1");
        NetworkInterfaceInventorySnapshot initial = await host.Service.ReconcileAsync(TestContext.CancellationToken);
        NetworkInterfaceInventoryItem eno1 = initial.Interfaces.Single();
        _ = await host.Service.UpdateCommentAsync(eno1.Id, "uplink", TestContext.CancellationToken);
        _ = await host.Service.UpdateVisibilityAsync(eno1.Id, isVisible: false, TestContext.CancellationToken);

        host.SetDaemonInterfaces();
        NetworkInterfaceInventorySnapshot missing = await host.Service.ReconcileAsync(TestContext.CancellationToken);
        NetworkInterfaceCleanupResult stale = await host.Service.GetStaleAsync(TestContext.CancellationToken);
        Assert.IsEmpty(missing.Interfaces);
        Assert.HasCount(1, stale.StaleInterfaces);
        Assert.AreEqual(eno1.Id, stale.StaleInterfaces[0].Id);

        host.SetDaemonInterfaces("eno1");
        NetworkInterfaceInventorySnapshot restored = await host.Service.ReconcileAsync(TestContext.CancellationToken);
        NetworkInterfaceCleanupResult afterRestore = await host.Service.GetStaleAsync(TestContext.CancellationToken);

        NetworkInterfaceInventoryItem restoredEno1 = restored.Interfaces.Single();
        Assert.AreEqual(eno1.Id, restoredEno1.Id);
        Assert.AreEqual("uplink", restoredEno1.Comment);
        Assert.IsFalse(restoredEno1.IsVisible);
        Assert.IsEmpty(afterRestore.StaleInterfaces);
    }

    [TestMethod]
    public async Task CleanupStaleAsync_ReappearedSelectedInterfaceIsReconciledInsteadOfDeletedAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetDaemonInterfaces("eno1");
        NetworkInterfaceInventorySnapshot initial = await host.Service.ReconcileAsync(TestContext.CancellationToken);
        NetworkInterfaceInventoryItem eno1 = initial.Interfaces.Single();
        _ = await host.Service.UpdateCommentAsync(eno1.Id, "retain", TestContext.CancellationToken);

        host.SetDaemonInterfaces();
        _ = await host.Service.ReconcileAsync(TestContext.CancellationToken);
        host.SetDaemonInterfaces("eno1");

        NetworkInterfaceCleanupResult cleanup = await host.Service.CleanupStaleAsync([eno1.Id], TestContext.CancellationToken);
        NetworkInterfaceInventorySnapshot present = await host.Service.GetCachedAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, cleanup.RemovedCount);
        Assert.IsEmpty(cleanup.StaleInterfaces);
        Assert.HasCount(1, present.Interfaces);
        Assert.AreEqual(eno1.Id, present.Interfaces[0].Id);
        Assert.AreEqual("retain", present.Interfaces[0].Comment);
    }

    [TestMethod]
    public async Task CleanupStaleAsync_PermanentlyDeletesSelectedInterfacesThatRemainAbsentAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetDaemonInterfaces("eno1", "wlan0");
        NetworkInterfaceInventorySnapshot initial = await host.Service.ReconcileAsync(TestContext.CancellationToken);
        Guid eno1Id = initial.Interfaces.Single(static item => item.Name == "eno1").Id;
        Guid wlan0Id = initial.Interfaces.Single(static item => item.Name == "wlan0").Id;

        host.SetDaemonInterfaces();
        _ = await host.Service.ReconcileAsync(TestContext.CancellationToken);
        NetworkInterfaceCleanupResult cleanup = await host.Service.CleanupStaleAsync([eno1Id], TestContext.CancellationToken);

        Assert.AreEqual(1, cleanup.RemovedCount);
        Assert.HasCount(1, cleanup.StaleInterfaces);
        Assert.AreEqual(wlan0Id, cleanup.StaleInterfaces[0].Id);
        Assert.AreEqual("wlan0", cleanup.StaleInterfaces[0].Name);
        Assert.IsFalse(cleanup.StaleInterfaces.Any(item => item.Id == eno1Id));
    }

    [TestMethod]
    public async Task ReconcileAsync_UsesSharedWkgTransactionScopeAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetDaemonInterfaces("eno1");

        _ = await host.Service.ReconcileAsync(TestContext.CancellationToken);

        Assert.AreEqual(TransactionState.Commit, host.TransactionService.Scoped.State);
    }

    [TestMethod]
    public async Task ReconcileAsync_EmptyDaemonInventoryPersistsReconciliationStateAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetDaemonInterfaces();

        NetworkInterfaceInventorySnapshot reconciled = await host.Service.ReconcileAsync(TestContext.CancellationToken);
        NetworkInterfaceInventorySnapshot cached = await host.Service.GetCachedAsync(TestContext.CancellationToken);

        Assert.IsEmpty(reconciled.Interfaces);
        Assert.AreEqual(host.Clock.GetUtcNow(), reconciled.ReconciledAt);
        Assert.AreEqual(reconciled.ReconciledAt, cached.ReconciledAt);
    }

    [TestMethod]
    public async Task ReconcileAsync_DuplicateDaemonNamesFailWithoutChangingCachedInventoryAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetDaemonInterfaces("eno1");
        NetworkInterfaceInventorySnapshot initial = await host.Service.ReconcileAsync(TestContext.CancellationToken);

        host.SetDaemonInterfaces("eno1", "eno1");
        await Assert.ThrowsExactlyAsync<DaemonInvalidResponseException>(() => host.Service.ReconcileAsync(TestContext.CancellationToken));

        NetworkInterfaceInventorySnapshot cached = await host.Service.GetCachedAsync(TestContext.CancellationToken);
        Assert.HasCount(1, cached.Interfaces);
        Assert.AreEqual(initial.Interfaces[0].Id, cached.Interfaces[0].Id);
        Assert.AreEqual(initial.ReconciledAt, cached.ReconciledAt);
    }

    [TestMethod]
    public async Task ReconcileAsync_PreservesDottedVlanInterfaceNameAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetDaemonInterfaces("eno1", "enp4s0f2.1100");

        NetworkInterfaceInventorySnapshot reconciled = await host.Service.ReconcileAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(s_vlanInterfaceNames, reconciled.Interfaces.Select(static item => item.Name).ToArray());
    }

    [TestMethod]
    public async Task UpdateCommentAsync_UnknownPublicIdReturnsNullAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);

        NetworkInterfaceInventorySnapshot? response = await host.Service.UpdateCommentAsync(Guid.CreateVersion7(), "missing", TestContext.CancellationToken);

        Assert.IsNull(response);
    }

    [TestMethod]
    public async Task UpdateVisibilityAsync_UnknownPublicIdReturnsNullAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);

        NetworkInterfaceInventorySnapshot? response = await host.Service.UpdateVisibilityAsync(Guid.CreateVersion7(), isVisible: false, TestContext.CancellationToken);

        Assert.IsNull(response);
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _services;
        private readonly AsyncServiceScope _scope;
        private readonly Mock<IUfwClient> _ufwClient;

        private TestHost(
            SqliteConnection connection,
            ServiceProvider services,
            AsyncServiceScope scope,
            Mock<IUfwClient> ufwClient,
            MutableTimeProvider clock,
            ITransactionService<ApplicationDbContext> transactionService,
            NetworkInterfaceInventoryService service)
        {
            _connection = connection;
            _services = services;
            _scope = scope;
            _ufwClient = ufwClient;
            Clock = clock;
            TransactionService = transactionService;
            Service = service;
        }

        public MutableTimeProvider Clock { get; }

        public ITransactionService<ApplicationDbContext> TransactionService { get; }

        public NetworkInterfaceInventoryService Service { get; }

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

            Mock<IUfwClient> ufwClient = new();
            MutableTimeProvider clock = new(new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.Zero));
            ITransactionServiceHandle transactionHandle = scope.ServiceProvider.GetRequiredService<ITransactionServiceHandle>();
            ITransactionService<ApplicationDbContext> transactionService = scope.ServiceProvider.GetRequiredService<ITransactionService<ApplicationDbContext>>();
            NetworkInterfaceDaemonGateway daemonGateway = new(ufwClient.Object);
            NetworkInterfaceDataAccess dataAccess = new(transactionHandle);
            NetworkInterfaceInventoryService service = new(daemonGateway, dataAccess, clock);
            return new TestHost(connection, serviceProvider, scope, ufwClient, clock, transactionService, service);
        }

        public void SetDaemonInterfaces(params string[] names) => _ufwClient
            .Setup(client => client.TrySendAsync<NetworkInterfaceListResponse>(RequestMethod.Get, "/api/v1/network-interfaces", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UfwIpcResult<NetworkInterfaceListResponse>.Success(new NetworkInterfaceListResponse(names)));

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _services.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }
}
