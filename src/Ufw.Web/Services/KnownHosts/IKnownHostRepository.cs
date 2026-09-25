using Ufw.Shared.Firewall;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Services.KnownHosts;

internal interface IKnownHostRepository
{
    Task<KnownHostInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<KnownHostInventoryItem?> GetByIdAsync(Guid publicId, CancellationToken cancellationToken = default);

    Task<KnownHostMutationResult> CreateAsync(
        string name,
        string normalizedName,
        string address,
        KnownHostAddressSource addressSource,
        DateTimeOffset? dnsResolvedAt,
        string? comment,
        bool isVisible,
        CancellationToken cancellationToken = default);

    Task<KnownHostMutationResult> UpdateAsync(
        Guid publicId,
        string name,
        string normalizedName,
        string address,
        FirewallAddressFamily addressFamily,
        KnownHostAddressSource addressSource,
        DateTimeOffset? dnsResolvedAt,
        string? comment,
        bool isVisible,
        CancellationToken cancellationToken = default);

    Task<KnownHostMutationResult> ReconcileDnsAsync(
        Guid publicId,
        string expectedName,
        string expectedAddress,
        DateTimeOffset? expectedResolvedAt,
        string address,
        FirewallAddressFamily addressFamily,
        DateTimeOffset resolvedAt,
        CancellationToken cancellationToken = default);

    Task<KnownHostMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default);
}
