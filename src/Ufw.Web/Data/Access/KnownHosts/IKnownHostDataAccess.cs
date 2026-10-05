using Ufw.Shared.Firewall;
using Ufw.Shared.Management.KnownHosts;

namespace Ufw.Web.Data.Access.KnownHosts;

internal interface IKnownHostDataAccess
{
    Task<IReadOnlyList<KnownHostInventoryItem>> GetAsync(CancellationToken cancellationToken = default);

    Task<KnownHostInventoryItem?> GetByIdAsync(Guid publicId, CancellationToken cancellationToken = default);

    Task<DataMutationResult> CreateAsync(
        string name,
        string normalizedName,
        string address,
        KnownHostAddressSource addressSource,
        DateTimeOffset? dnsResolvedAt,
        string? comment,
        bool isVisible,
        CancellationToken cancellationToken = default);

    Task<DataMutationResult> UpdateAsync(
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

    Task<DataMutationResult> ReconcileDnsAsync(
        Guid publicId,
        string expectedName,
        string expectedAddress,
        DateTimeOffset? expectedResolvedAt,
        string address,
        FirewallAddressFamily addressFamily,
        DateTimeOffset resolvedAt,
        CancellationToken cancellationToken = default);

    Task<DataMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default);
}
