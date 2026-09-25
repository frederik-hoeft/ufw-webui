using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.UI.Components.Hosts;

internal interface IKnownHostEditorDialogService
{
    Task<KnownHostInventoryResponse?> CreateAsync(string? initialAddress = null, CancellationToken cancellationToken = default);

    Task<KnownHostInventoryResponse?> EditAsync(KnownHostInventoryItem host, CancellationToken cancellationToken = default);
}
