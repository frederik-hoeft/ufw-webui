using Ufw.Web.Client.Features.KnownHosts.Api;

namespace Ufw.Web.Client.Features.KnownHosts;

public interface IKnownHostInventoryService
{
    KnownHostInventoryResponse? Current { get; }

    Task<KnownHostInventoryResponse> RefreshAsync(CancellationToken cancellationToken = default);

    Task<KnownHostInventoryResponse> CreateAsync(CreateKnownHostRequest request, CancellationToken cancellationToken = default);

    Task<KnownHostInventoryResponse> UpdateAsync(Guid hostId, UpdateKnownHostRequest request, CancellationToken cancellationToken = default);

    Task<KnownHostInventoryResponse> DeleteAsync(Guid hostId, CancellationToken cancellationToken = default);
}
