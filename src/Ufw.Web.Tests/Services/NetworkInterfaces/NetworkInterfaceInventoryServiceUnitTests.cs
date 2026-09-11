using Ufw.Web.Api.V1.Models.NetworkInterfaces;
using Ufw.Web.Data.Model;
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
        NetworkInterfaceInventoryResponse expected = new([], s_now);
        RecordingDaemonSource daemonSource = new(interfaceNames);
        RecordingRepository repository = new() { ReconcileResult = expected };
        NetworkInterfaceInventoryService service = new(daemonSource, repository, new FixedTimeProvider(s_now));

        NetworkInterfaceInventoryResponse result = await service.ReconcileAsync();

        Assert.AreSame(expected, result);
        Assert.AreSame(interfaceNames, repository.ReconciledNames);
        Assert.AreEqual(s_now, repository.ReconciledAt);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task UpdateCommentAsync_BlankCommentNormalizesToNullAsync(string? comment)
    {
        Guid publicId = Guid.CreateVersion7();
        NetworkInterfaceInventoryResponse expected = new([], null);
        RecordingRepository repository = new() { CommentResult = expected };
        NetworkInterfaceInventoryService service = new(new RecordingDaemonSource([]), repository, TimeProvider.System);

        NetworkInterfaceInventoryResponse? result = await service.UpdateCommentAsync(publicId, comment);

        Assert.AreSame(expected, result);
        Assert.AreEqual(publicId, repository.CommentPublicId);
        Assert.IsNull(repository.Comment);
    }

    [TestMethod]
    public async Task UpdateCommentAsync_NonBlankCommentIsTrimmedBeforePersistenceAsync()
    {
        Guid publicId = Guid.CreateVersion7();
        NetworkInterfaceInventoryResponse expected = new([], null);
        RecordingRepository repository = new() { CommentResult = expected };
        NetworkInterfaceInventoryService service = new(new RecordingDaemonSource([]), repository, TimeProvider.System);

        NetworkInterfaceInventoryResponse? result = await service.UpdateCommentAsync(publicId, "  management VLAN  ");

        Assert.AreSame(expected, result);
        Assert.AreEqual(publicId, repository.CommentPublicId);
        Assert.AreEqual("management VLAN", repository.Comment);
    }

    [TestMethod]
    public async Task UpdateCommentAsync_OverlongCommentIsRejectedBeforeRepositoryAccessAsync()
    {
        RecordingRepository repository = new();
        NetworkInterfaceInventoryService service = new(new RecordingDaemonSource([]), repository, TimeProvider.System);
        string comment = new('x', NetworkInterfaceEntry.MAX_COMMENT_LENGTH + 1);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.UpdateCommentAsync(Guid.CreateVersion7(), comment));

        Assert.AreEqual(0, repository.UpdateCommentCallCount);
    }

    [TestMethod]
    public async Task UpdateVisibilityAsync_DelegatesPublicIdAndVisibilityWithoutTransformationAsync()
    {
        Guid publicId = Guid.CreateVersion7();
        NetworkInterfaceInventoryResponse expected = new([], null);
        RecordingRepository repository = new() { VisibilityResult = expected };
        NetworkInterfaceInventoryService service = new(new RecordingDaemonSource([]), repository, TimeProvider.System);

        NetworkInterfaceInventoryResponse? result = await service.UpdateVisibilityAsync(publicId, isVisible: false);

        Assert.AreSame(expected, result);
        Assert.AreEqual(publicId, repository.VisibilityPublicId);
        Assert.IsFalse(repository.IsVisible);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingDaemonSource(IReadOnlyList<string> interfaceNames) : IDaemonNetworkInterfaceSource
    {
        public Task<IReadOnlyList<string>> GetInterfaceNamesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(interfaceNames);
        }
    }

    private sealed class RecordingRepository : INetworkInterfaceInventoryRepository
    {
        public NetworkInterfaceInventoryResponse ReconcileResult { get; init; } = new([], null);

        public NetworkInterfaceInventoryResponse? CommentResult { get; init; }

        public NetworkInterfaceInventoryResponse? VisibilityResult { get; init; }

        public IReadOnlyList<string>? ReconciledNames { get; private set; }

        public DateTimeOffset ReconciledAt { get; private set; }

        public Guid CommentPublicId { get; private set; }

        public string? Comment { get; private set; }

        public int UpdateCommentCallCount { get; private set; }

        public Guid VisibilityPublicId { get; private set; }

        public bool IsVisible { get; private set; }

        public Task<NetworkInterfaceInventoryResponse> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new NetworkInterfaceInventoryResponse([], null));

        public Task<NetworkInterfaceInventoryResponse> ReconcileAsync(
            IReadOnlyList<string> currentNames,
            DateTimeOffset reconciledAt,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReconciledNames = currentNames;
            ReconciledAt = reconciledAt;
            return Task.FromResult(ReconcileResult);
        }

        public Task<NetworkInterfaceInventoryResponse?> UpdateCommentAsync(
            Guid publicId,
            string? comment,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UpdateCommentCallCount++;
            CommentPublicId = publicId;
            Comment = comment;
            return Task.FromResult(CommentResult);
        }

        public Task<NetworkInterfaceInventoryResponse?> UpdateVisibilityAsync(
            Guid publicId,
            bool isVisible,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VisibilityPublicId = publicId;
            IsVisible = isVisible;
            return Task.FromResult(VisibilityResult);
        }
    }
}
