using Ufw.Ipc.Client;
using Ufw.Shared.Management.NetworkInterfaces;
using Ufw.Web.Data.Access;
using Ufw.Web.Data.Access.NetworkInterfaces;
using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Services.NetworkInterfaces;

internal sealed class NetworkInterfaceInventoryService(
    INetworkInterfaceDaemonGateway daemonGateway,
    INetworkInterfaceDataAccess dataAccess,
    TimeProvider timeProvider) : INetworkInterfaceInventoryService
{
    public Task<NetworkInterfaceInventorySnapshot> GetCachedAsync(CancellationToken cancellationToken = default) =>
        dataAccess.GetPresentAsync(cancellationToken);

    public async Task<NetworkInterfaceInventorySnapshot> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> currentNames = await GetAuthoritativeNamesAsync(cancellationToken);
        await dataAccess.ReconcileAsync(currentNames, timeProvider.GetUtcNow(), cancellationToken);
        return await dataAccess.GetPresentAsync(cancellationToken);
    }

    public async Task<NetworkInterfaceInventorySnapshot?> UpdateCommentAsync(Guid publicId, string? comment, CancellationToken cancellationToken = default)
    {
        DataMutationResult result = await dataAccess.UpdateCommentAsync(publicId, NormalizeComment(comment), cancellationToken);
        return await MapMutationAsync(result, cancellationToken);
    }

    public async Task<NetworkInterfaceInventorySnapshot?> UpdateVisibilityAsync(Guid publicId, bool isVisible, CancellationToken cancellationToken = default)
    {
        DataMutationResult result = await dataAccess.UpdateVisibilityAsync(publicId, isVisible, cancellationToken);
        return await MapMutationAsync(result, cancellationToken);
    }

    public async Task<NetworkInterfaceCleanupResult> GetStaleAsync(CancellationToken cancellationToken = default)
    {
        NetworkInterfaceInventorySnapshot snapshot = await dataAccess.GetStaleAsync(cancellationToken);
        return new NetworkInterfaceCleanupResult(snapshot.Interfaces, snapshot.ReconciledAt);
    }

    public async Task<NetworkInterfaceCleanupResult> CleanupStaleAsync(IReadOnlyCollection<Guid> interfaceIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interfaceIds);
        Guid[] selectedIds = [.. interfaceIds.Distinct().Order()];
        IReadOnlyList<string> currentNames = await GetAuthoritativeNamesAsync(cancellationToken);
        int removedCount = await dataAccess.ReconcileAndDeleteStaleAsync(currentNames, selectedIds, timeProvider.GetUtcNow(), cancellationToken);
        NetworkInterfaceInventorySnapshot stale = await dataAccess.GetStaleAsync(cancellationToken);
        return new NetworkInterfaceCleanupResult(stale.Interfaces, stale.ReconciledAt, removedCount);
    }

    private async Task<IReadOnlyList<string>> GetAuthoritativeNamesAsync(CancellationToken cancellationToken)
    {
        DaemonResult<IReadOnlyList<string>> daemonResult = await daemonGateway.GetInterfaceNamesAsync(cancellationToken);
        if (!daemonResult.TryGetResult(out IReadOnlyList<string>? currentNames, out UfwIpcError? error))
        {
            throw new DaemonUnavailableException(error);
        }
        return currentNames;
    }

    private async Task<NetworkInterfaceInventorySnapshot?> MapMutationAsync(DataMutationResult result, CancellationToken cancellationToken)
    {
        if (result.IsSuccess)
        {
            return await dataAccess.GetPresentAsync(cancellationToken);
        }
        if (result.Error is DataMutationNotFoundError)
        {
            return null;
        }

        throw new InvalidOperationException($"Unexpected network-interface data mutation error '{result.Error!.GetType().Name}'.");
    }

    private static string? NormalizeComment(string? comment) => string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
}
