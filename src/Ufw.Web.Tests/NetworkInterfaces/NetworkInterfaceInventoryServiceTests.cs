using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Api.V1.Models.NetworkInterfaces;
using Ufw.Web.Data;
using Ufw.Web.Services.NetworkInterfaces;

namespace Ufw.Web.Tests.NetworkInterfaces;

[TestClass]
public sealed class NetworkInterfaceInventoryServiceTests
{
    private static readonly string[] s_vlanInterfaceNames = ["eno1", "enp4s0f2.1100"];

    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ReconcileAsync_PreservesMetadataForExistingInterfacesAndRemovesMissingEntriesAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetDaemonInterfaces("docker0", "eno1");

        NetworkInterfaceInventoryResponse initial = await host.Service.ReconcileAsync(TestContext.CancellationToken);
        NetworkInterfaceInventoryItem eno1 = initial.Interfaces.Single(static item => item.Name == "eno1");
        Assert.AreEqual('7', eno1.Id.ToString("D")[14]);

        NetworkInterfaceInventoryResponse? commented = await host.Service.UpdateCommentAsync(
            eno1.Id,
            " service VLAN ",
            TestContext.CancellationToken);
        Assert.IsNotNull(commented);
        Assert.AreEqual("service VLAN", commented.Interfaces.Single(static item => item.Name == "eno1").Comment);

        host.Clock.Advance(TimeSpan.FromMinutes(1));
        host.SetDaemonInterfaces("eno1", "wlan0");
        NetworkInterfaceInventoryResponse reconciled = await host.Service.ReconcileAsync(TestContext.CancellationToken);

        Assert.HasCount(2, reconciled.Interfaces);
        NetworkInterfaceInventoryItem retained = reconciled.Interfaces.Single(static item => item.Name == "eno1");
        Assert.AreEqual(eno1.Id, retained.Id);
        Assert.AreEqual("service VLAN", retained.Comment);
        Assert.IsFalse(reconciled.Interfaces.Any(static item => item.Name == "docker0"));
        Assert.AreEqual('7', reconciled.Interfaces.Single(static item => item.Name == "wlan0").Id.ToString("D")[14]);
        Assert.AreEqual(host.Clock.GetUtcNow(), reconciled.ReconciledAt);
    }

    [TestMethod]
    public async Task ReconcileAsync_EmptyDaemonInventoryPersistsReconciliationStateAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetDaemonInterfaces();

        NetworkInterfaceInventoryResponse reconciled = await host.Service.ReconcileAsync(TestContext.CancellationToken);
        NetworkInterfaceInventoryResponse cached = await host.Service.GetCachedAsync(TestContext.CancellationToken);

        Assert.IsEmpty(reconciled.Interfaces);
        Assert.AreEqual(host.Clock.GetUtcNow(), reconciled.ReconciledAt);
        Assert.AreEqual(reconciled.ReconciledAt, cached.ReconciledAt);
    }

    [TestMethod]
    public async Task ReconcileAsync_DuplicateDaemonNamesFailWithoutChangingCachedInventoryAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetDaemonInterfaces("eno1");
        NetworkInterfaceInventoryResponse initial = await host.Service.ReconcileAsync(TestContext.CancellationToken);

        host.SetDaemonInterfaces("eno1", "eno1");
        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => host.Service.ReconcileAsync(TestContext.CancellationToken));

        NetworkInterfaceInventoryResponse cached = await host.Service.GetCachedAsync(TestContext.CancellationToken);
        Assert.HasCount(1, cached.Interfaces);
        Assert.AreEqual(initial.Interfaces[0].Id, cached.Interfaces[0].Id);
        Assert.AreEqual(initial.ReconciledAt, cached.ReconciledAt);
    }

    [TestMethod]
    public async Task ReconcileAsync_PreservesDottedVlanInterfaceNameAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetDaemonInterfaces("eno1", "enp4s0f2.1100");

        NetworkInterfaceInventoryResponse reconciled = await host.Service.ReconcileAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            s_vlanInterfaceNames,
            reconciled.Interfaces.Select(static item => item.Name).ToArray());
    }

    [TestMethod]
    public async Task UpdateCommentAsync_UnknownPublicIdReturnsNullAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);

        NetworkInterfaceInventoryResponse? response = await host.Service.UpdateCommentAsync(
            Guid.CreateVersion7(),
            "missing",
            TestContext.CancellationToken);

        Assert.IsNull(response);
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly Mock<IUfwClient> _ufwClient;

        private TestHost(
            SqliteConnection connection,
            ApplicationDbContext context,
            Mock<IUfwClient> ufwClient,
            MutableTimeProvider clock)
        {
            _connection = connection;
            _context = context;
            _ufwClient = ufwClient;
            Clock = clock;
            Service = new NetworkInterfaceInventoryService(context, ufwClient.Object, clock);
        }

        public MutableTimeProvider Clock { get; }

        public NetworkInterfaceInventoryService Service { get; }

        public static async Task<TestHost> CreateAsync(CancellationToken cancellationToken)
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync(cancellationToken);
            DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            ApplicationDbContext context = new(options, new ApplicationModelLoader());
            await context.Database.EnsureCreatedAsync(cancellationToken);

            Mock<IUfwClient> ufwClient = new();
            MutableTimeProvider clock = new(new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.Zero));
            return new TestHost(connection, context, ufwClient, clock);
        }

        public void SetDaemonInterfaces(params string[] names) => _ufwClient
            .Setup(client => client.SendAsync<NetworkInterfaceListResponse>(
                RequestMethod.Get,
                "/api/v1/network-interfaces",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NetworkInterfaceListResponse(names));

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
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
