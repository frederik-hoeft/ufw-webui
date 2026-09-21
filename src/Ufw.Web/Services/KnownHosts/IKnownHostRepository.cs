using Ufw.Shared.Firewall;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Services.KnownHosts;

internal interface IKnownHostRepository
{
    Task<KnownHostInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<KnownHostMutationResult> CreateAsync(string name, string normalizedName, string address, string? comment, bool isVisible, CancellationToken cancellationToken = default);

    Task<KnownHostMutationResult> UpdateAsync(
        Guid publicId,
        string name,
        string normalizedName,
        string address,
        FirewallAddressFamily addressFamily,
        string? comment,
        bool isVisible,
        CancellationToken cancellationToken = default);

    Task<KnownHostMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default);
}
