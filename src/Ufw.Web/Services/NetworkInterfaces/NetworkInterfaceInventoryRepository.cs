using Microsoft.EntityFrameworkCore;
using Ufw.Web.Api.V1.Models.NetworkInterfaces;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;

namespace Ufw.Web.Services.NetworkInterfaces;

internal sealed class NetworkInterfaceInventoryRepository(ITransactionServiceHandle transactionService)
    : DatabaseService<ApplicationDbContext>(transactionService), INetworkInterfaceInventoryRepository
{
    public Task<NetworkInterfaceInventoryResponse> GetAsync(CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunReadOnlyAsync(context => GetCoreAsync(context, cancellationToken));

    public Task<NetworkInterfaceInventoryResponse> ReconcileAsync(IReadOnlyList<string> currentNames, DateTimeOffset reconciledAt, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<NetworkInterfaceInventoryResponse>(async (context, transaction) =>
        {
            ArgumentNullException.ThrowIfNull(currentNames);
            HashSet<string> currentNameSet = new(currentNames, StringComparer.Ordinal);
            List<NetworkInterfaceEntry> cached = await context.Set<NetworkInterfaceEntry>().ToListAsync(cancellationToken);
            Dictionary<string, NetworkInterfaceEntry> cachedByName = cached.ToDictionary(static networkInterface => networkInterface.Name, StringComparer.Ordinal);

            foreach (NetworkInterfaceEntry existing in cached)
            {
                if (!currentNameSet.Contains(existing.Name))
                {
                    context.Remove(existing);
                }
            }

            foreach (string name in currentNames)
            {
                if (!cachedByName.ContainsKey(name))
                {
                    context.Add(new NetworkInterfaceEntry { Name = name });
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

            await context.SaveChangesAsync(cancellationToken);
            NetworkInterfaceInventoryResponse response = await GetCoreAsync(context, cancellationToken);
            return transaction.Commit(response);
        });

    public Task<NetworkInterfaceInventoryResponse?> UpdateCommentAsync(Guid publicId, string? comment, CancellationToken cancellationToken = default) =>
        UpdateEntryAsync(publicId, networkInterface => networkInterface.Comment = comment, cancellationToken);

    public Task<NetworkInterfaceInventoryResponse?> UpdateVisibilityAsync(Guid publicId, bool isVisible, CancellationToken cancellationToken = default) =>
        UpdateEntryAsync(publicId, networkInterface => networkInterface.IsVisible = isVisible, cancellationToken);

    private Task<NetworkInterfaceInventoryResponse?> UpdateEntryAsync(Guid publicId, Action<NetworkInterfaceEntry> update, CancellationToken cancellationToken) =>
        Transaction.Scoped.RunAsync<NetworkInterfaceInventoryResponse?>(async (context, transaction) =>
        {
            NetworkInterfaceEntry? networkInterface = await context.Set<NetworkInterfaceEntry>().SingleOrDefaultAsync(entry => entry.PublicId == publicId, cancellationToken);
            if (networkInterface is null)
            {
                return transaction.Rollback<NetworkInterfaceInventoryResponse?>(null);
            }

            update(networkInterface);
            await context.SaveChangesAsync(cancellationToken);
            NetworkInterfaceInventoryResponse response = await GetCoreAsync(context, cancellationToken);
            return transaction.Commit<NetworkInterfaceInventoryResponse?>(response);
        });

    private static async Task<NetworkInterfaceInventoryResponse> GetCoreAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        NetworkInterfaceInventoryItem[] interfaces = await context.Set<NetworkInterfaceEntry>()
            .AsNoTracking()
            .OrderBy(static networkInterface => networkInterface.Name)
            .Select(static networkInterface => new NetworkInterfaceInventoryItem(networkInterface.PublicId, networkInterface.Name, networkInterface.Comment, networkInterface.IsVisible))
            .ToArrayAsync(cancellationToken);
        DateTimeOffset? reconciledAt = await context.Set<NetworkInterfaceCacheState>()
            .AsNoTracking()
            .Where(static state => state.Id == NetworkInterfaceCacheState.SINGLETON_ID)
            .Select(static state => (DateTimeOffset?)state.ReconciledAt)
            .SingleOrDefaultAsync(cancellationToken);

        return new NetworkInterfaceInventoryResponse(interfaces, reconciledAt);
    }
}
