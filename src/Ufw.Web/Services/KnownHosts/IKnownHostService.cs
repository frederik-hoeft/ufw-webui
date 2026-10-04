using Ufw.Shared.Management.KnownHosts;
using Ufw.Web.Data.Access;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Services.KnownHosts;

public interface IKnownHostService
{
    Task<IReadOnlyList<KnownHostInventoryItem>> GetAsync(CancellationToken cancellationToken = default);

    Task<DataMutationResult<IReadOnlyList<KnownHostInventoryItem>>> CreateAsync(CreateKnownHostRequest request, CancellationToken cancellationToken = default);

    Task<DataMutationResult<IReadOnlyList<KnownHostInventoryItem>>> UpdateAsync(Guid publicId, UpdateKnownHostRequest request, CancellationToken cancellationToken = default);

    Task<DataMutationResult<IReadOnlyList<KnownHostInventoryItem>>> ReconcileDnsAsync(Guid publicId, CancellationToken cancellationToken = default);

    Task<DataMutationResult<IReadOnlyList<KnownHostInventoryItem>>> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default);
}
