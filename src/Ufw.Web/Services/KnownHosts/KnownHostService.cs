using Ufw.Shared.Firewall;
using Ufw.Web.Data.Model;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Services.KnownHosts;

internal sealed class KnownHostService(IKnownHostRepository repository, IKnownHostDnsResolver dnsResolver, TimeProvider timeProvider) : IKnownHostService
{
    public Task<KnownHostInventoryResponse> GetAsync(CancellationToken cancellationToken = default) =>
        repository.GetAsync(cancellationToken);

    public async Task<KnownHostMutationResult> CreateAsync(CreateKnownHostRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryNormalizeMetadata(request.Name, request.Comment, out KnownHostMetadata metadata))
        {
            return new KnownHostMutationResult(KnownHostMutationOutcome.InvalidAddress);
        }
        if (!HasValidAddressSourceConfiguration(request))
        {
            return new KnownHostMutationResult(KnownHostMutationOutcome.InvalidDnsConfiguration);
        }

        KnownHostAddressValues? address = await ResolveAddressAsync(request, currentHost: null, cancellationToken);
        if (address is null)
        {
            return new KnownHostMutationResult(AddressFailureOutcome(request));
        }

        return await repository.CreateAsync(
            metadata.Name,
            metadata.NormalizedName,
            address.Value.Address,
            request.AddressSource,
            address.Value.DnsResolvedAt,
            metadata.Comment,
            request.IsVisible,
            cancellationToken);
    }

    public async Task<KnownHostMutationResult> UpdateAsync(Guid publicId, UpdateKnownHostRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryNormalizeMetadata(request.Name, request.Comment, out KnownHostMetadata metadata))
        {
            return new KnownHostMutationResult(KnownHostMutationOutcome.InvalidAddress);
        }
        if (!HasValidAddressSourceConfiguration(request))
        {
            return new KnownHostMutationResult(KnownHostMutationOutcome.InvalidDnsConfiguration);
        }

        KnownHostInventoryItem? currentHost = await repository.GetByIdAsync(publicId, cancellationToken);
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
                return new KnownHostMutationResult(AddressFailureOutcome(request));
            }
        }

        return await repository.UpdateAsync(
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
    }

    public async Task<KnownHostMutationResult> ReconcileDnsAsync(Guid publicId, CancellationToken cancellationToken = default)
    {
        KnownHostInventoryItem? host = await repository.GetByIdAsync(publicId, cancellationToken);
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

        return await repository.ReconcileDnsAsync(
            publicId,
            host.Name,
            host.Address,
            host.DnsResolvedAt,
            address,
            host.AddressFamily,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public Task<KnownHostMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default) =>
        repository.DeleteAsync(publicId, cancellationToken);

    private async Task<KnownHostAddressValues?> ResolveAddressAsync(KnownHostRequest request, KnownHostInventoryItem? currentHost, CancellationToken cancellationToken)
    {
        if (request.AddressSource == KnownHostAddressSource.Literal)
        {
            if (!FirewallAddressValue.TryNormalizeLiteral(request.Address, out string? normalizedAddress, out FirewallAddressFamily addressFamily))
            {
                return null;
            }

            return new KnownHostAddressValues(normalizedAddress, addressFamily, DnsResolvedAt: null);
        }
        if (request.AddressSource != KnownHostAddressSource.Dns || request.DnsAddressFamily is not { } dnsAddressFamily)
        {
            return null;
        }

        string? resolvedAddress = await dnsResolver.ResolveAsync(request.Name.Trim(), dnsAddressFamily, currentHost?.Address, cancellationToken);
        return resolvedAddress is null
            ? null
            : new KnownHostAddressValues(resolvedAddress, dnsAddressFamily, timeProvider.GetUtcNow());
    }

    private static bool HasValidAddressSourceConfiguration(KnownHostRequest request) => request.AddressSource switch
    {
        KnownHostAddressSource.Literal => request.DnsAddressFamily is null,
        KnownHostAddressSource.Dns => request.Address is null && request.DnsAddressFamily is FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6,
        _ => false,
    };

    private static KnownHostMutationOutcome AddressFailureOutcome(KnownHostRequest request) => request.AddressSource switch
    {
        KnownHostAddressSource.Literal => KnownHostMutationOutcome.InvalidAddress,
        KnownHostAddressSource.Dns => KnownHostMutationOutcome.DnsResolutionFailed,
        _ => KnownHostMutationOutcome.InvalidDnsConfiguration,
    };

    private static bool TryNormalizeMetadata(string name, string? comment, out KnownHostMetadata metadata)
    {
        string normalizedName = name.Trim();
        string lookupName = normalizedName.ToUpperInvariant();
        if (normalizedName.Length is 0 or > KnownHostEntry.MAX_NAME_LENGTH || lookupName.Length > KnownHostEntry.MAX_NAME_LENGTH)
        {
            metadata = default;
            return false;
        }

        string? normalizedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (normalizedComment?.Length > KnownHostEntry.MAX_COMMENT_LENGTH)
        {
            metadata = default;
            return false;
        }

        metadata = new KnownHostMetadata(normalizedName, lookupName, normalizedComment);
        return true;
    }

    private readonly record struct KnownHostMetadata(string Name, string NormalizedName, string? Comment);
    private readonly record struct KnownHostAddressValues(string Address, FirewallAddressFamily AddressFamily, DateTimeOffset? DnsResolvedAt);
}
