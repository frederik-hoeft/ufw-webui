using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ufw.Shared.Firewall;
using Ufw.Web.Model.V1.KnownHosts;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Wkg.AspNetCore.Abstractions.Services;
using Wkg.AspNetCore.Transactions;

namespace Ufw.Web.Services.KnownHosts;

internal sealed class KnownHostRepository(ITransactionServiceHandle transactionService)
    : DatabaseService<ApplicationDbContext>(transactionService), IKnownHostRepository
{
    public Task<KnownHostInventoryResponse> GetAsync(CancellationToken cancellationToken = default) =>
        Transaction.Scoped.RunReadOnlyAsync(context => GetCoreAsync(context, cancellationToken));

    public Task<KnownHostMutationResult> CreateAsync(string name, string normalizedName, string address, string? comment, bool isVisible, CancellationToken cancellationToken = default) =>
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
        KnownHostProjection[] hosts = await context.Set<KnownHostEntry>()
            .AsNoTracking()
            .OrderBy(static host => host.NormalizedName)
            .ThenBy(static host => host.Name)
            .Select(static host => new KnownHostProjection(host.PublicId, host.Name, host.Address, host.Comment, host.IsVisible))
            .ToArrayAsync(cancellationToken);

        return new KnownHostInventoryResponse { Hosts = [.. hosts.Select(ToInventoryItem)] };
    }

    private static KnownHostInventoryItem ToInventoryItem(KnownHostProjection host)
    {
        if (!FirewallAddressValue.TryNormalizeLiteral(host.Address, out string? address, out FirewallAddressFamily addressFamily))
        {
            throw new InvalidDataException($"Known host '{host.Id:D}' contains an invalid persisted address.");
        }

        return new KnownHostInventoryItem
        {
            Id = host.Id,
            Name = host.Name,
            Address = address,
            AddressFamily = addressFamily,
            Comment = host.Comment,
            IsVisible = host.IsVisible,
        };
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private sealed record KnownHostProjection(Guid Id, string Name, string Address, string? Comment, bool IsVisible);
}
