using Ufw.Ipc.Client;
using Ufw.Shared.Management.NetworkInterfaces;
using Ufw.Web.Data.Access;
using Ufw.Web.Data.Access.NetworkInterfaces;
using Ufw.Web.Services.Daemon;
using Ufw.Web.Services.NetworkInterfaces;

namespace Ufw.Web.Tests.Services.NetworkInterfaces;

[TestClass]
public sealed class NetworkInterfaceInventoryServiceUnitTests
{
    private static readonly DateTimeOffset s_now = new(2026, 9, 11, 18, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task ReconcileAsync_UsesDaemonInventoryAndCurrentUtcTimeAsync()
    {
        string[] interfaceNames = ["eno1", "wlan0"];
        NetworkInterfaceInventorySnapshot expected = new([], s_now);
        RecordingDaemonGateway daemonGateway = new(interfaceNames);
        RecordingDataAccess dataAccess = new() { PresentResult = expected };
        NetworkInterfaceInventoryService service = new(daemonGateway, dataAccess, new FixedTimeProvider(s_now));

        NetworkInterfaceInventorySnapshot result = await service.ReconcileAsync();

        Assert.AreSame(expected, result);
        Assert.AreSame(interfaceNames, dataAccess.ReconciledNames);
        Assert.AreEqual(s_now, dataAccess.ReconciledAt);
    }

    [TestMethod]
    public async Task ReconcileAsync_DaemonFailureIsClassifiedAsUnavailableBeforeDataAccessAsync()
    {
        UfwIpcError ipcError = new(400, "enumeration failed");
        RecordingDataAccess dataAccess = new();
        NetworkInterfaceInventoryService service = new(new FailingDaemonGateway(ipcError), dataAccess, TimeProvider.System);

        DaemonUnavailableException exception = await Assert.ThrowsExactlyAsync<DaemonUnavailableException>(() => service.ReconcileAsync());

        Assert.AreSame(ipcError, exception.Error);
        Assert.IsNull(dataAccess.ReconciledNames);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task UpdateCommentAsync_BlankCommentNormalizesToNullAsync(string? comment)
    {
        Guid publicId = Guid.CreateVersion7();
        NetworkInterfaceInventorySnapshot expected = new([], null);
        RecordingDataAccess dataAccess = new() { PresentResult = expected };
        NetworkInterfaceInventoryService service = new(new RecordingDaemonGateway([]), dataAccess, TimeProvider.System);

        NetworkInterfaceInventorySnapshot? result = await service.UpdateCommentAsync(publicId, comment);

        Assert.AreSame(expected, result);
        Assert.AreEqual(publicId, dataAccess.CommentPublicId);
        Assert.IsNull(dataAccess.Comment);
    }

    [TestMethod]
    public async Task UpdateCommentAsync_NonBlankCommentIsTrimmedBeforePersistenceAsync()
    {
        Guid publicId = Guid.CreateVersion7();
        NetworkInterfaceInventorySnapshot expected = new([], null);
        RecordingDataAccess dataAccess = new() { PresentResult = expected };
        NetworkInterfaceInventoryService service = new(new RecordingDaemonGateway([]), dataAccess, TimeProvider.System);

        NetworkInterfaceInventorySnapshot? result = await service.UpdateCommentAsync(publicId, "  management VLAN  ");

        Assert.AreSame(expected, result);
        Assert.AreEqual(publicId, dataAccess.CommentPublicId);
        Assert.AreEqual("management VLAN", dataAccess.Comment);
    }

    [TestMethod]
    public async Task UpdateVisibilityAsync_DelegatesPublicIdAndVisibilityWithoutTransformationAsync()
    {
        Guid publicId = Guid.CreateVersion7();
        NetworkInterfaceInventorySnapshot expected = new([], null);
        RecordingDataAccess dataAccess = new() { PresentResult = expected };
        NetworkInterfaceInventoryService service = new(new RecordingDaemonGateway([]), dataAccess, TimeProvider.System);

        NetworkInterfaceInventorySnapshot? result = await service.UpdateVisibilityAsync(publicId, isVisible: false);

        Assert.AreSame(expected, result);
        Assert.AreEqual(publicId, dataAccess.VisibilityPublicId);
        Assert.IsFalse(dataAccess.IsVisible);
    }

    [TestMethod]
    public async Task CleanupStaleAsync_RevalidatesAgainstDaemonBeforeDeletingSelectedMetadataAsync()
    {
        Guid first = Guid.CreateVersion7();
        Guid second = Guid.CreateVersion7();
        string[] interfaceNames = ["eno1"];
        NetworkInterfaceInventorySnapshot stale = new([new NetworkInterfaceInventoryItem(second, "wlan0", "old", isVisible: false)], s_now);
        RecordingDataAccess dataAccess = new() { StaleResult = stale, CleanupRemovedCount = 1 };
        NetworkInterfaceInventoryService service = new(new RecordingDaemonGateway(interfaceNames), dataAccess, new FixedTimeProvider(s_now));

        NetworkInterfaceCleanupResult result = await service.CleanupStaleAsync([second, first, second]);

        Assert.AreEqual(1, result.RemovedCount);
        Assert.AreSame(stale.Interfaces, result.StaleInterfaces);
        Assert.AreEqual(s_now, result.ReconciledAt);
        Assert.AreSame(interfaceNames, dataAccess.CleanupNames);
        CollectionAssert.AreEqual(new[] { first, second }.Order().ToArray(), dataAccess.CleanupIds!.ToArray());
        Assert.AreEqual(s_now, dataAccess.CleanupReconciledAt);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingDaemonGateway(IReadOnlyList<string> interfaceNames) : INetworkInterfaceDaemonGateway
    {
        public Task<DaemonResult<IReadOnlyList<string>>> GetInterfaceNamesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(DaemonResult.Success(interfaceNames));
        }
    }

    private sealed class FailingDaemonGateway(UfwIpcError error) : INetworkInterfaceDaemonGateway
    {
        public Task<DaemonResult<IReadOnlyList<string>>> GetInterfaceNamesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(DaemonResult.Failure<IReadOnlyList<string>>(error));
        }
    }

    private sealed class RecordingDataAccess : INetworkInterfaceDataAccess
    {
        public NetworkInterfaceInventorySnapshot PresentResult { get; init; } = new([], null);

        public NetworkInterfaceInventorySnapshot StaleResult { get; init; } = new([], null);

        public DataMutationResult MutationResult { get; init; } = DataMutationResult.Success();

        public int CleanupRemovedCount { get; init; }

        public IReadOnlyList<string>? ReconciledNames { get; private set; }

        public DateTimeOffset ReconciledAt { get; private set; }

        public IReadOnlyList<string>? CleanupNames { get; private set; }

        public IReadOnlyCollection<Guid>? CleanupIds { get; private set; }

        public DateTimeOffset CleanupReconciledAt { get; private set; }

        public Guid CommentPublicId { get; private set; }

        public string? Comment { get; private set; }

        public Guid VisibilityPublicId { get; private set; }

        public bool IsVisible { get; private set; }

        public Task<NetworkInterfaceInventorySnapshot> GetPresentAsync(CancellationToken cancellationToken = default) => Task.FromResult(PresentResult);

        public Task<NetworkInterfaceInventorySnapshot> GetStaleAsync(CancellationToken cancellationToken = default) => Task.FromResult(StaleResult);

        public Task ReconcileAsync(IReadOnlyList<string> currentNames, DateTimeOffset reconciledAt, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReconciledNames = currentNames;
            ReconciledAt = reconciledAt;
            return Task.CompletedTask;
        }

        public Task<int> ReconcileAndDeleteStaleAsync(
            IReadOnlyList<string> currentNames,
            IReadOnlyCollection<Guid> selectedIds,
            DateTimeOffset reconciledAt,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CleanupNames = currentNames;
            CleanupIds = selectedIds;
            CleanupReconciledAt = reconciledAt;
            return Task.FromResult(CleanupRemovedCount);
        }

        public Task<DataMutationResult> UpdateCommentAsync(Guid publicId, string? comment, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CommentPublicId = publicId;
            Comment = comment;
            return Task.FromResult(MutationResult);
        }

        public Task<DataMutationResult> UpdateVisibilityAsync(Guid publicId, bool isVisible, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VisibilityPublicId = publicId;
            IsVisible = isVisible;
            return Task.FromResult(MutationResult);
        }
    }
}
