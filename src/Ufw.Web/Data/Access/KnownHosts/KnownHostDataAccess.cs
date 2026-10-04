using Microsoft.EntityFrameworkCore;
using Ufw.Shared.Firewall;
using Ufw.Shared.Management.KnownHosts;
using Ufw.Web.Data.Extensions;
using Ufw.Web.Data.Model;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;

namespace Ufw.Web.Data.Access.KnownHosts;

internal sealed class KnownHostDataAccess(ITransactionServiceHandle transactionService) : DatabaseService<ApplicationDbContext>(transactionService), IKnownHostDataAccess
{
    public Task<IReadOnlyList<KnownHostInventoryItem>> GetAsync(CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunReadOnlyAsync(context => GetCoreAsync(context, cancellationToken));

    public Task<KnownHostInventoryItem?> GetByIdAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunReadOnlyAsync(async context =>
        {
            IQueryable<KnownHostEntry> query = context.Set<KnownHostEntry>()
                .AsNoTracking()
                .Where(candidate => candidate.PublicId == publicId);
            KnownHostInventoryItem? host = await ProjectInventory(query).SingleOrDefaultAsync(cancellationToken);
            return host;
        });

    public Task<DataMutationResult> CreateAsync(
        string name,
        string normalizedName,
        string address,
        KnownHostAddressSource addressSource,
        DateTimeOffset? dnsResolvedAt,
        string? comment,
        bool isVisible,
        CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync(async (context, transaction) =>
        {
            bool duplicateName = await context.Set<KnownHostEntry>().AnyAsync(host => host.NormalizedName == normalizedName, cancellationToken);
            if (duplicateName)
            {
                return transaction.Rollback(DataMutationResult.Failure(new DataMutationUniqueConflictError()));
            }

            context.Add(new KnownHostEntry
            {
                Name = name,
                NormalizedName = normalizedName,
                Address = address,
                AddressSource = addressSource,
                DnsResolvedAt = dnsResolvedAt,
                Comment = comment,
                IsVisible = isVisible,
            });

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.TryGetDataMutationError(out DataMutationError? error) && error is DataMutationUniqueConflictError)
            {
                return transaction.Rollback(DataMutationResult.Failure(error));
            }

            return transaction.Commit(DataMutationResult.Success());
        });

    public Task<DataMutationResult> UpdateAsync(
        Guid publicId,
        string name,
        string normalizedName,
        string address,
        FirewallAddressFamily addressFamily,
        KnownHostAddressSource addressSource,
        DateTimeOffset? dnsResolvedAt,
        string? comment,
        bool isVisible,
        CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync(async (context, transaction) =>
        {
            KnownHostEntry? host = await context.Set<KnownHostEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == publicId, cancellationToken);
            if (host is null)
            {
                return transaction.Rollback(DataMutationResult.Failure(new DataMutationNotFoundError()));
            }
            if (!FirewallAddressValue.TryNormalizeLiteral(host.Address, out _, out FirewallAddressFamily existingFamily))
            {
                throw new InvalidDataException($"Known host '{host.PublicId:D}' contains an invalid persisted address.");
            }
            if (existingFamily != addressFamily)
            {
                return transaction.Rollback(DataMutationResult.Failure(new KnownHostAddressFamilyConflictError()));
            }

            bool nameConflict = await context.Set<KnownHostEntry>()
                .AnyAsync(candidate => candidate.Id != host.Id && candidate.NormalizedName == normalizedName, cancellationToken);
            if (nameConflict)
            {
                return transaction.Rollback(DataMutationResult.Failure(new DataMutationUniqueConflictError()));
            }

            host.Name = name;
            host.NormalizedName = normalizedName;
            host.Address = address;
            host.AddressSource = addressSource;
            host.DnsResolvedAt = dnsResolvedAt;
            host.Comment = comment;
            host.IsVisible = isVisible;

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.TryGetDataMutationError(out DataMutationError? error) && error is DataMutationUniqueConflictError)
            {
                return transaction.Rollback(DataMutationResult.Failure(error));
            }

            return transaction.Commit(DataMutationResult.Success());
        });

    public Task<DataMutationResult> ReconcileDnsAsync(
        Guid publicId,
        string expectedName,
        string expectedAddress,
        DateTimeOffset? expectedResolvedAt,
        string address,
        FirewallAddressFamily addressFamily,
        DateTimeOffset resolvedAt,
        CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync(async (context, transaction) =>
        {
            KnownHostEntry? host = await context.Set<KnownHostEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == publicId, cancellationToken);
            if (host is null)
            {
                return transaction.Rollback(DataMutationResult.Failure(new DataMutationNotFoundError()));
            }
            if (host.AddressSource != KnownHostAddressSource.Dns)
            {
                return transaction.Rollback(DataMutationResult.Failure(new KnownHostNotDnsManagedError()));
            }
            if (!string.Equals(host.Name, expectedName, StringComparison.Ordinal)
                || !string.Equals(host.Address, expectedAddress, StringComparison.Ordinal)
                || host.DnsResolvedAt != expectedResolvedAt)
            {
                return transaction.Rollback(DataMutationResult.Failure(new KnownHostDnsConfigurationChangedError()));
            }
            if (!FirewallAddressValue.TryNormalizeLiteral(host.Address, out _, out FirewallAddressFamily existingFamily))
            {
                throw new InvalidDataException($"Known host '{host.PublicId:D}' contains an invalid persisted address.");
            }
            if (existingFamily != addressFamily)
            {
                return transaction.Rollback(DataMutationResult.Failure(new KnownHostAddressFamilyConflictError()));
            }

            host.Address = address;
            host.DnsResolvedAt = resolvedAt;
            await context.SaveChangesAsync(cancellationToken);
            return transaction.Commit(DataMutationResult.Success());
        });

    public Task<DataMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync(async (context, transaction) =>
        {
            KnownHostEntry? host = await context.Set<KnownHostEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == publicId, cancellationToken);
            if (host is null)
            {
                return transaction.Rollback(DataMutationResult.Failure(new DataMutationNotFoundError()));
            }

            context.Remove(host);
            await context.SaveChangesAsync(cancellationToken);
            return transaction.Commit(DataMutationResult.Success());
        });

    private static async Task<IReadOnlyList<KnownHostInventoryItem>> GetCoreAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        IQueryable<KnownHostEntry> query = context.Set<KnownHostEntry>()
            .AsNoTracking()
            .OrderBy(static host => host.NormalizedName)
            .ThenBy(static host => host.Name);
        return await ProjectInventory(query).ToListAsync(cancellationToken);
    }

    private static IQueryable<KnownHostInventoryItem> ProjectInventory(IQueryable<KnownHostEntry> query) => query
        .Select(static host => ToInventoryItem(host.PublicId, host.Name, host.Address, host.AddressSource, host.DnsResolvedAt, host.Comment, host.IsVisible));

    private static KnownHostInventoryItem ToInventoryItem(
        Guid id,
        string name,
        string persistedAddress,
        KnownHostAddressSource addressSource,
        DateTimeOffset? dnsResolvedAt,
        string? comment,
        bool isVisible)
    {
        if (!FirewallAddressValue.TryNormalizeLiteral(persistedAddress, out string? address, out FirewallAddressFamily addressFamily))
        {
            throw new InvalidDataException($"Known host '{id:D}' contains an invalid persisted address.");
        }
        if (!Enum.IsDefined(addressSource)
            || addressSource == KnownHostAddressSource.Literal && dnsResolvedAt is not null
            || addressSource == KnownHostAddressSource.Dns && dnsResolvedAt is null)
        {
            throw new InvalidDataException($"Known host '{id:D}' contains invalid address-source metadata.");
        }

        return new KnownHostInventoryItem
        {
            Id = id,
            Name = name,
            Address = address,
            AddressFamily = addressFamily,
            AddressSource = addressSource,
            DnsResolvedAt = dnsResolvedAt,
            Comment = comment,
            IsVisible = isVisible,
        };
    }
}
