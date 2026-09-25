using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Services.KnownHosts;

public interface IKnownHostService
{
    Task<KnownHostInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<KnownHostMutationResult> CreateAsync(CreateKnownHostRequest request, CancellationToken cancellationToken = default);

    Task<KnownHostMutationResult> UpdateAsync(Guid publicId, UpdateKnownHostRequest request, CancellationToken cancellationToken = default);

    Task<KnownHostMutationResult> ReconcileDnsAsync(Guid publicId, CancellationToken cancellationToken = default);

    Task<KnownHostMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default);
}
