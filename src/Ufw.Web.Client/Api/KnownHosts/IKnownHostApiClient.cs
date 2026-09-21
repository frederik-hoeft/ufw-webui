using Ufw.Web.Client.Api.KnownHosts.Model;
namespace Ufw.Web.Client.Api.KnownHosts;

public interface IKnownHostApiClient
{
    Task<KnownHostInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<KnownHostInventoryResponse> CreateAsync(CreateKnownHostRequest request, CancellationToken cancellationToken = default);

    Task<KnownHostInventoryResponse> UpdateAsync(Guid hostId, UpdateKnownHostRequest request, CancellationToken cancellationToken = default);

    Task<KnownHostInventoryResponse> DeleteAsync(Guid hostId, CancellationToken cancellationToken = default);
}
