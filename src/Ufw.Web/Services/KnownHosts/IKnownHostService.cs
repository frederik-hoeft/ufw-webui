using Ufw.Web.Api.V1.Models.KnownHosts;

namespace Ufw.Web.Services.KnownHosts;

public interface IKnownHostService
{
    Task<KnownHostInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<KnownHostMutationResult> CreateAsync(CreateKnownHostRequest request, CancellationToken cancellationToken = default);

    Task<KnownHostMutationResult> UpdateAsync(Guid publicId, UpdateKnownHostRequest request, CancellationToken cancellationToken = default);

    Task<KnownHostMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default);
}
