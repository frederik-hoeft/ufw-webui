using Ufw.Shared.Firewall;
using Ufw.Shared.Management.KnownHosts;
using Ufw.Web.Data.Access;
using Ufw.Web.Data.Access.KnownHosts;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Services.KnownHosts;

internal sealed class KnownHostService(IKnownHostDataAccess dataAccess, IKnownHostDnsResolver dnsResolver, TimeProvider timeProvider) : IKnownHostService
{
    public Task<IReadOnlyList<KnownHostInventoryItem>> GetAsync(CancellationToken cancellationToken = default) =>
        dataAccess.GetAsync(cancellationToken);

    public async Task<KnownHostMutationResult> CreateAsync(CreateKnownHostRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        KnownHostMetadata metadata = NormalizeMetadata(request.Name, request.Comment);
        KnownHostAddressValues? address = await ResolveAddressAsync(request, currentHost: null, cancellationToken);
        if (address is null)
        {
            return new KnownHostMutationResult(KnownHostMutationOutcome.DnsResolutionFailed);
        }

        DataMutationResult result = await dataAccess.CreateAsync(
            metadata.Name,
            metadata.NormalizedName,
            address.Value.Address,
            request.AddressSource,
            address.Value.DnsResolvedAt,
            metadata.Comment,
            request.IsVisible,
            cancellationToken);
        return await MapDataMutationAsync(result, cancellationToken);
    }

    public async Task<KnownHostMutationResult> UpdateAsync(Guid publicId, UpdateKnownHostRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        KnownHostMetadata metadata = NormalizeMetadata(request.Name, request.Comment);
        KnownHostInventoryItem? currentHost = await dataAccess.GetByIdAsync(publicId, cancellationToken);
        if (currentHost is null)
        {
            return new KnownHostMutationResult(KnownHostMutationOutcome.NotFound);
        }

        KnownHostAddressValues? address;
        if (request.AddressSource == KnownHostAddressSource.Dns
            && currentHost.AddressSource == KnownHostAddressSource.Dns
            && string.Equals(currentHost.Name, metadata.Name, StringComparison.Ordinal)
            && request.DnsAddressFamily == currentHost.AddressFamily)
        {
            address = new KnownHostAddressValues(currentHost.Address, currentHost.AddressFamily, currentHost.DnsResolvedAt);
        }
        else
        {
            if (request.AddressSource == KnownHostAddressSource.Dns && request.DnsAddressFamily != currentHost.AddressFamily)
            {
                return new KnownHostMutationResult(KnownHostMutationOutcome.AddressFamilyConflict);
            }

            address = await ResolveAddressAsync(request, currentHost, cancellationToken);
            if (address is null)
            {
                return new KnownHostMutationResult(KnownHostMutationOutcome.DnsResolutionFailed);
            }
        }

        DataMutationResult result = await dataAccess.UpdateAsync(
            publicId,
            metadata.Name,
            metadata.NormalizedName,
            address.Value.Address,
            address.Value.AddressFamily,
            request.AddressSource,
            address.Value.DnsResolvedAt,
            metadata.Comment,
            request.IsVisible,
            cancellationToken);
        return await MapDataMutationAsync(result, cancellationToken);
    }

    public async Task<KnownHostMutationResult> ReconcileDnsAsync(Guid publicId, CancellationToken cancellationToken = default)
    {
        KnownHostInventoryItem? host = await dataAccess.GetByIdAsync(publicId, cancellationToken);
        if (host is null)
        {
            return new KnownHostMutationResult(KnownHostMutationOutcome.NotFound);
        }
        if (host.AddressSource != KnownHostAddressSource.Dns)
        {
            return new KnownHostMutationResult(KnownHostMutationOutcome.NotDnsManaged);
        }

        string? address = await dnsResolver.ResolveAsync(host.Name, host.AddressFamily, host.Address, cancellationToken);
        if (address is null)
        {
            return new KnownHostMutationResult(KnownHostMutationOutcome.DnsResolutionFailed);
        }

        DataMutationResult result = await dataAccess.ReconcileDnsAsync(
            publicId,
            host.Name,
            host.Address,
            host.DnsResolvedAt,
            address,
            host.AddressFamily,
            timeProvider.GetUtcNow(),
            cancellationToken);
        return await MapDataMutationAsync(result, cancellationToken);
    }

    public async Task<KnownHostMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default)
    {
        DataMutationResult result = await dataAccess.DeleteAsync(publicId, cancellationToken);
        return await MapDataMutationAsync(result, cancellationToken);
    }

    private async Task<KnownHostAddressValues?> ResolveAddressAsync(KnownHostRequest request, KnownHostInventoryItem? currentHost, CancellationToken cancellationToken)
    {
        if (request.AddressSource == KnownHostAddressSource.Literal)
        {
            if (!FirewallAddressValue.TryNormalizeLiteral(request.Address, out string? normalizedAddress, out FirewallAddressFamily addressFamily))
            {
                throw new ArgumentException("Literal known-host address must be validated before application processing.", nameof(request));
            }

            return new KnownHostAddressValues(normalizedAddress, addressFamily, DnsResolvedAt: null);
        }
        if (request.AddressSource != KnownHostAddressSource.Dns || request.DnsAddressFamily is not { } dnsAddressFamily)
        {
            throw new ArgumentException("Known-host address configuration must be validated before application processing.", nameof(request));
        }

        string? resolvedAddress = await dnsResolver.ResolveAsync(request.Name.Trim(), dnsAddressFamily, currentHost?.Address, cancellationToken);
        return resolvedAddress is null
            ? null
            : new KnownHostAddressValues(resolvedAddress, dnsAddressFamily, timeProvider.GetUtcNow());
    }

    private async Task<KnownHostMutationResult> MapDataMutationAsync(DataMutationResult result, CancellationToken cancellationToken)
    {
        if (result.IsSuccess)
        {
            IReadOnlyList<KnownHostInventoryItem> inventory = await dataAccess.GetAsync(cancellationToken);
            return new KnownHostMutationResult(KnownHostMutationOutcome.Success, inventory);
        }

        KnownHostMutationOutcome outcome = result.Error switch
        {
            DataMutationNotFoundError => KnownHostMutationOutcome.NotFound,
            DataMutationUniqueConflictError => KnownHostMutationOutcome.NameConflict,
            KnownHostAddressFamilyConflictError => KnownHostMutationOutcome.AddressFamilyConflict,
            KnownHostNotDnsManagedError => KnownHostMutationOutcome.NotDnsManaged,
            KnownHostDnsConfigurationChangedError => KnownHostMutationOutcome.DnsConfigurationChanged,
            _ => throw new InvalidOperationException($"Unexpected known-host data mutation error '{result.Error!.GetType().Name}'."),
        };
        return new KnownHostMutationResult(outcome);
    }

    private static KnownHostMetadata NormalizeMetadata(string name, string? comment)
    {
        string normalizedName = name.Trim();
        string? normalizedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        return new KnownHostMetadata(normalizedName, normalizedName.ToUpperInvariant(), normalizedComment);
    }

    private readonly record struct KnownHostMetadata(string Name, string NormalizedName, string? Comment);
    private readonly record struct KnownHostAddressValues(string Address, FirewallAddressFamily AddressFamily, DateTimeOffset? DnsResolvedAt);
}
