using Microsoft.EntityFrameworkCore;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Api.V1.Models.NetworkInterfaces;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;

namespace Ufw.Web.Services.NetworkInterfaces;

internal sealed class NetworkInterfaceInventoryService(
    ITransactionServiceHandle transactionService,
    IUfwClient ufwClient,
    TimeProvider timeProvider) : DatabaseService<ApplicationDbContext>(transactionService), INetworkInterfaceInventoryService
{
    public Task<NetworkInterfaceInventoryResponse> GetCachedAsync(CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunReadOnlyAsync(context => GetCachedCoreAsync(context, cancellationToken));

    public async Task<NetworkInterfaceInventoryResponse> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        // Do not hold a database transaction open while waiting on the daemon. The returned
        // names are validated before the transactional cache update begins.
        NetworkInterfaceListResponse daemonResponse = await ufwClient.SendAsync<NetworkInterfaceListResponse>(RequestMethod.Get, "/api/v1/network-interfaces", cancellationToken);
        string[] currentNames = ValidateAndOrderDaemonNames(daemonResponse.Interfaces);

        return await Transaction.Scoped.RunAsync<NetworkInterfaceInventoryResponse>(async (context, transaction) =>
        {
            HashSet<string> currentNameSet = new(currentNames, StringComparer.Ordinal);
            List<NetworkInterfaceEntry> cached = await context.Set<NetworkInterfaceEntry>()
                .ToListAsync(cancellationToken);
            Dictionary<string, NetworkInterfaceEntry> cachedByName = cached.ToDictionary(
                static networkInterface => networkInterface.Name,
                StringComparer.Ordinal);

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

            DateTimeOffset reconciledAt = timeProvider.GetUtcNow();
            NetworkInterfaceCacheState? state = await context.Set<NetworkInterfaceCacheState>()
                .SingleOrDefaultAsync(
                    static state => state.Id == NetworkInterfaceCacheState.SINGLETON_ID,
                    cancellationToken);
            if (state is null)
            {
                context.Add(new NetworkInterfaceCacheState { ReconciledAt = reconciledAt });
            }
            else
            {
                state.ReconciledAt = reconciledAt;
            }

            await context.SaveChangesAsync(cancellationToken);
            NetworkInterfaceInventoryResponse response = await GetCachedCoreAsync(context, cancellationToken);
            return transaction.Commit(response);
        });
    }

    public Task<NetworkInterfaceInventoryResponse?> UpdateCommentAsync(Guid publicId, string? comment, CancellationToken cancellationToken = default) =>
        UpdateEntryAsync(
            publicId,
            (networkInterface) => networkInterface.Comment = NormalizeComment(comment),
            cancellationToken);

    public Task<NetworkInterfaceInventoryResponse?> UpdateVisibilityAsync(Guid publicId, bool isVisible, CancellationToken cancellationToken = default) =>
        UpdateEntryAsync(
            publicId,
            networkInterface => networkInterface.IsVisible = isVisible,
            cancellationToken);

    private Task<NetworkInterfaceInventoryResponse?> UpdateEntryAsync(Guid publicId, Action<NetworkInterfaceEntry> update, CancellationToken cancellationToken) =>
        Transaction.Scoped.RunAsync<NetworkInterfaceInventoryResponse?>(async (context, transaction) =>
        {
            NetworkInterfaceEntry? networkInterface = await context.Set<NetworkInterfaceEntry>()
                .SingleOrDefaultAsync(entry => entry.PublicId == publicId, cancellationToken);
            if (networkInterface is null)
            {
                return transaction.Rollback<NetworkInterfaceInventoryResponse?>(null);
            }

            update(networkInterface);
            await context.SaveChangesAsync(cancellationToken);
            NetworkInterfaceInventoryResponse response = await GetCachedCoreAsync(context, cancellationToken);
            return transaction.Commit<NetworkInterfaceInventoryResponse?>(response);
        });

    private static async Task<NetworkInterfaceInventoryResponse> GetCachedCoreAsync(ApplicationDbContext context, CancellationToken cancellationToken)
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

    private static string[] ValidateAndOrderDaemonNames(IReadOnlyList<string>? names)
    {
        if (names is null)
        {
            throw new InvalidDataException("Daemon network-interface response is missing the interface list.");
        }

        if (names.Any(static name => string.IsNullOrWhiteSpace(name)))
        {
            throw new InvalidDataException("Daemon network-interface response contains an invalid interface name.");
        }

        if (names.Any(static name => name.Length > NetworkInterfaceEntry.MAX_NAME_LENGTH))
        {
            throw new InvalidDataException("Daemon returned a network-interface name that exceeds the supported length.");
        }

        string[] ordered = names
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidDataException("Daemon network-interface response contains duplicate interface names.");
        }

        return ordered;
    }

    private static string? NormalizeComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return null;
        }

        string normalized = comment.Trim();
        if (normalized.Length > NetworkInterfaceEntry.MAX_COMMENT_LENGTH)
        {
            throw new ArgumentException($"Interface comments must not exceed {NetworkInterfaceEntry.MAX_COMMENT_LENGTH} characters.", nameof(comment));
        }

        return normalized;
    }
}
