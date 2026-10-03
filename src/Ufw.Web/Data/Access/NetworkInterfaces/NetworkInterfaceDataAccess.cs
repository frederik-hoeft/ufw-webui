using Microsoft.EntityFrameworkCore;
using Ufw.Shared.Management.NetworkInterfaces;
using Ufw.Web.Data.Model;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;

namespace Ufw.Web.Data.Access.NetworkInterfaces;

internal sealed class NetworkInterfaceDataAccess(ITransactionServiceHandle transactionService)
    : DatabaseService<ApplicationDbContext>(transactionService), INetworkInterfaceDataAccess
{
    public Task<NetworkInterfaceInventorySnapshot> GetPresentAsync(CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunReadOnlyAsync(context => GetSnapshotAsync(context, isPresent: true, cancellationToken));

    public Task<NetworkInterfaceInventorySnapshot> GetStaleAsync(CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunReadOnlyAsync(context => GetSnapshotAsync(context, isPresent: false, cancellationToken));

    public Task ReconcileAsync(IReadOnlyList<string> currentNames, DateTimeOffset reconciledAt, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync(async (context, transaction) =>
        {
            await ReconcileCoreAsync(context, currentNames, reconciledAt, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return transaction.Commit();
        });

    public Task<int> ReconcileAndDeleteStaleAsync(
        IReadOnlyList<string> currentNames,
        IReadOnlyCollection<Guid> selectedIds,
        DateTimeOffset reconciledAt,
        CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<int>(async (context, transaction) =>
        {
            ArgumentNullException.ThrowIfNull(selectedIds);
            IReadOnlyList<NetworkInterfaceEntry> cached = await ReconcileCoreAsync(context, currentNames, reconciledAt, cancellationToken);
            HashSet<Guid> selected = new(selectedIds);
            NetworkInterfaceEntry[] deleted = [.. cached.Where(entry => !entry.IsPresent && selected.Contains(entry.PublicId))];
            context.RemoveRange(deleted);
            await context.SaveChangesAsync(cancellationToken);
            return transaction.Commit(deleted.Length);
        });

    public Task<DataMutationResult> UpdateCommentAsync(Guid publicId, string? comment, CancellationToken cancellationToken = default) =>
        UpdateEntryAsync(publicId, networkInterface => networkInterface.Comment = comment, cancellationToken);

    public Task<DataMutationResult> UpdateVisibilityAsync(Guid publicId, bool isVisible, CancellationToken cancellationToken = default) =>
        UpdateEntryAsync(publicId, networkInterface => networkInterface.IsVisible = isVisible, cancellationToken);

    private Task<DataMutationResult> UpdateEntryAsync(Guid publicId, Action<NetworkInterfaceEntry> update, CancellationToken cancellationToken) =>
        Transaction.Scoped.RunAsync<DataMutationResult>(async (context, transaction) =>
        {
            NetworkInterfaceEntry? networkInterface = await context.Set<NetworkInterfaceEntry>().SingleOrDefaultAsync(entry => entry.PublicId == publicId && entry.IsPresent, cancellationToken);
            if (networkInterface is null)
            {
                return transaction.Rollback(DataMutationResult.Failure(new DataMutationNotFoundError()));
            }

            update(networkInterface);
            await context.SaveChangesAsync(cancellationToken);
            return transaction.Commit(DataMutationResult.Success());
        });

    private static async Task<IReadOnlyList<NetworkInterfaceEntry>> ReconcileCoreAsync(
        ApplicationDbContext context,
        IReadOnlyList<string> currentNames,
        DateTimeOffset reconciledAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currentNames);
        HashSet<string> currentNameSet = new(currentNames, StringComparer.Ordinal);
        List<NetworkInterfaceEntry> cached = await context.Set<NetworkInterfaceEntry>().ToListAsync(cancellationToken);
        Dictionary<string, NetworkInterfaceEntry> cachedByName = cached.ToDictionary(static networkInterface => networkInterface.Name, StringComparer.Ordinal);

        foreach (NetworkInterfaceEntry existing in cached)
        {
            existing.IsPresent = currentNameSet.Contains(existing.Name);
        }

        foreach (string name in currentNames)
        {
            if (!cachedByName.ContainsKey(name))
            {
                context.Add(new NetworkInterfaceEntry { Name = name, IsPresent = true });
            }
        }

        NetworkInterfaceCacheState? state = await context.Set<NetworkInterfaceCacheState>()
            .SingleOrDefaultAsync(static state => state.Id == NetworkInterfaceCacheState.SINGLETON_ID, cancellationToken);
        if (state is null)
        {
            context.Add(new NetworkInterfaceCacheState { ReconciledAt = reconciledAt });
        }
        else
        {
            state.ReconciledAt = reconciledAt;
        }

        return cached;
    }

    private static async Task<NetworkInterfaceInventorySnapshot> GetSnapshotAsync(ApplicationDbContext context, bool isPresent, CancellationToken cancellationToken)
    {
        List<NetworkInterfaceInventoryItem> interfaces = await context.Set<NetworkInterfaceEntry>()
            .AsNoTracking()
            .Where(networkInterface => networkInterface.IsPresent == isPresent)
            .OrderBy(static networkInterface => networkInterface.Name)
            .Select(static networkInterface => new NetworkInterfaceInventoryItem
            {
                Id = networkInterface.PublicId,
                Name = networkInterface.Name,
                Comment = networkInterface.Comment,
                IsVisible = networkInterface.IsVisible,
            })
            .ToListAsync(cancellationToken);
        DateTimeOffset? reconciledAt = await context.Set<NetworkInterfaceCacheState>()
            .AsNoTracking()
            .Where(static state => state.Id == NetworkInterfaceCacheState.SINGLETON_ID)
            .Select(static state => (DateTimeOffset?)state.ReconciledAt)
            .SingleOrDefaultAsync(cancellationToken);

        return new NetworkInterfaceInventorySnapshot(interfaces, reconciledAt);
    }
}
