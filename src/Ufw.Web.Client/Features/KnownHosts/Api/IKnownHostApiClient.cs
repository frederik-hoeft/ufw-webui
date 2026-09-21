namespace Ufw.Web.Client.Features.KnownHosts.Api;

public interface IKnownHostApiClient
{
    Task<KnownHostInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<KnownHostInventoryResponse> CreateAsync(CreateKnownHostRequest request, CancellationToken cancellationToken = default);

    Task<KnownHostInventoryResponse> UpdateAsync(Guid hostId, UpdateKnownHostRequest request, CancellationToken cancellationToken = default);

    Task<KnownHostInventoryResponse> DeleteAsync(Guid hostId, CancellationToken cancellationToken = default);
}
