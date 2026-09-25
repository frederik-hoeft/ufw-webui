using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ufw.Shared.Firewall;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Ufw.Web.Model.V1.KnownHosts;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;

namespace Ufw.Web.Services.KnownHosts;

internal sealed class KnownHostRepository(ITransactionServiceHandle transactionService)
    : DatabaseService<ApplicationDbContext>(transactionService), IKnownHostRepository
{
    public Task<KnownHostInventoryResponse> GetAsync(CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunReadOnlyAsync(context => GetCoreAsync(context, cancellationToken));

    public Task<KnownHostInventoryItem?> GetByIdAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunReadOnlyAsync(async context =>
        {
            KnownHostProjection? host = await context.Set<KnownHostEntry>()
                .AsNoTracking()
                .Where(candidate => candidate.PublicId == publicId)
                .Select(static candidate => new KnownHostProjection(
                    candidate.PublicId,
                    candidate.Name,
                    candidate.Address,
                    candidate.AddressSource,
                    candidate.DnsResolvedAt,
                    candidate.Comment,
                    candidate.IsVisible))
                .SingleOrDefaultAsync(cancellationToken);
            return host is null ? null : ToInventoryItem(host);
        });

    public Task<KnownHostMutationResult> CreateAsync(
        string name,
        string normalizedName,
        string address,
        KnownHostAddressSource addressSource,
        DateTimeOffset? dnsResolvedAt,
        string? comment,
        bool isVisible,
        CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<KnownHostMutationResult>(async (context, transaction) =>
        {
            if (await context.Set<KnownHostEntry>().AnyAsync(host => host.NormalizedName == normalizedName, cancellationToken))
            {
                return transaction.Rollback(new KnownHostMutationResult(KnownHostMutationOutcome.NameConflict));
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
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return transaction.Rollback(new KnownHostMutationResult(KnownHostMutationOutcome.NameConflict));
            }

            KnownHostInventoryResponse response = await GetCoreAsync(context, cancellationToken);
            return transaction.Commit(new KnownHostMutationResult(KnownHostMutationOutcome.Success, response));
        });

    public Task<KnownHostMutationResult> UpdateAsync(
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
        Transaction.Scoped.RunAsync<KnownHostMutationResult>(async (context, transaction) =>
        {
            KnownHostEntry? host = await context.Set<KnownHostEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == publicId, cancellationToken);
            if (host is null)
            {
                return transaction.Rollback(new KnownHostMutationResult(KnownHostMutationOutcome.NotFound));
            }

            if (!FirewallAddressValue.TryNormalizeLiteral(host.Address, out _, out FirewallAddressFamily existingFamily))
            {
                throw new InvalidDataException($"Known host '{host.PublicId:D}' contains an invalid persisted address.");
            }
            if (existingFamily != addressFamily)
            {
                return transaction.Rollback(new KnownHostMutationResult(KnownHostMutationOutcome.AddressFamilyConflict));
            }

            bool nameConflict = await context.Set<KnownHostEntry>()
                .AnyAsync(candidate => candidate.Id != host.Id && candidate.NormalizedName == normalizedName, cancellationToken);
            if (nameConflict)
            {
                return transaction.Rollback(new KnownHostMutationResult(KnownHostMutationOutcome.NameConflict));
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
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return transaction.Rollback(new KnownHostMutationResult(KnownHostMutationOutcome.NameConflict));
            }

            KnownHostInventoryResponse response = await GetCoreAsync(context, cancellationToken);
            return transaction.Commit(new KnownHostMutationResult(KnownHostMutationOutcome.Success, response));
        });

    public Task<KnownHostMutationResult> ReconcileDnsAsync(
        Guid publicId,
        string expectedName,
        string expectedAddress,
        DateTimeOffset? expectedResolvedAt,
        string address,
        FirewallAddressFamily addressFamily,
        DateTimeOffset resolvedAt,
        CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<KnownHostMutationResult>(async (context, transaction) =>
        {
            KnownHostEntry? host = await context.Set<KnownHostEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == publicId, cancellationToken);
            if (host is null)
            {
                return transaction.Rollback(new KnownHostMutationResult(KnownHostMutationOutcome.NotFound));
            }
            if (host.AddressSource != KnownHostAddressSource.Dns)
            {
                return transaction.Rollback(new KnownHostMutationResult(KnownHostMutationOutcome.NotDnsManaged));
            }
            if (!string.Equals(host.Name, expectedName, StringComparison.Ordinal)
                || !string.Equals(host.Address, expectedAddress, StringComparison.Ordinal)
                || host.DnsResolvedAt != expectedResolvedAt)
            {
                return transaction.Rollback(new KnownHostMutationResult(KnownHostMutationOutcome.DnsConfigurationChanged));
            }
            if (!FirewallAddressValue.TryNormalizeLiteral(host.Address, out _, out FirewallAddressFamily existingFamily))
            {
                throw new InvalidDataException($"Known host '{host.PublicId:D}' contains an invalid persisted address.");
            }
            if (existingFamily != addressFamily)
            {
                return transaction.Rollback(new KnownHostMutationResult(KnownHostMutationOutcome.AddressFamilyConflict));
            }

            host.Address = address;
            host.DnsResolvedAt = resolvedAt;
            await context.SaveChangesAsync(cancellationToken);
            KnownHostInventoryResponse response = await GetCoreAsync(context, cancellationToken);
            return transaction.Commit(new KnownHostMutationResult(KnownHostMutationOutcome.Success, response));
        });

    public Task<KnownHostMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunAsync<KnownHostMutationResult>(async (context, transaction) =>
        {
            KnownHostEntry? host = await context.Set<KnownHostEntry>().SingleOrDefaultAsync(candidate => candidate.PublicId == publicId, cancellationToken);
            if (host is null)
            {
                return transaction.Rollback(new KnownHostMutationResult(KnownHostMutationOutcome.NotFound));
            }

            context.Remove(host);
            await context.SaveChangesAsync(cancellationToken);
            KnownHostInventoryResponse response = await GetCoreAsync(context, cancellationToken);
            return transaction.Commit(new KnownHostMutationResult(KnownHostMutationOutcome.Success, response));
        });

    private static async Task<KnownHostInventoryResponse> GetCoreAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        KnownHostProjection[] hosts = await Query(context).ToArrayAsync(cancellationToken);
        return new KnownHostInventoryResponse { Hosts = [.. hosts.Select(ToInventoryItem)] };
    }

    private static IQueryable<KnownHostProjection> Query(ApplicationDbContext context) => context.Set<KnownHostEntry>()
        .AsNoTracking()
        .OrderBy(static host => host.NormalizedName)
        .ThenBy(static host => host.Name)
        .Select(static host => new KnownHostProjection(
            host.PublicId,
            host.Name,
            host.Address,
            host.AddressSource,
            host.DnsResolvedAt,
            host.Comment,
            host.IsVisible));

    private static KnownHostInventoryItem ToInventoryItem(KnownHostProjection host)
    {
        if (!FirewallAddressValue.TryNormalizeLiteral(host.Address, out string? address, out FirewallAddressFamily addressFamily))
        {
            throw new InvalidDataException($"Known host '{host.Id:D}' contains an invalid persisted address.");
        }
        if (!Enum.IsDefined(host.AddressSource)
            || host.AddressSource == KnownHostAddressSource.Literal && host.DnsResolvedAt is not null
            || host.AddressSource == KnownHostAddressSource.Dns && host.DnsResolvedAt is null)
        {
            throw new InvalidDataException($"Known host '{host.Id:D}' contains invalid address-source metadata.");
        }

        return new KnownHostInventoryItem
        {
            Id = host.Id,
            Name = host.Name,
            Address = address,
            AddressFamily = addressFamily,
            AddressSource = host.AddressSource,
            DnsResolvedAt = host.DnsResolvedAt,
            Comment = host.Comment,
            IsVisible = host.IsVisible,
        };
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private sealed record KnownHostProjection(
        Guid Id,
        string Name,
        string Address,
        KnownHostAddressSource AddressSource,
        DateTimeOffset? DnsResolvedAt,
        string? Comment,
        bool IsVisible);
}
