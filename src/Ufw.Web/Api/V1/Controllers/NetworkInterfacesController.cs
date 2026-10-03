using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Model.V1.NetworkInterfaces;
using Ufw.Web.Services.NetworkInterfaces;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class NetworkInterfacesController(INetworkInterfaceInventoryService inventory) : ControllerBase
{
    public async partial Task<ActionResult<NetworkInterfaceInventoryResponse>> GetAsync(CancellationToken cancellationToken)
    {
        NetworkInterfaceInventoryResponse response = await inventory.GetCachedAsync(cancellationToken);
        return Ok(response);
    }

    public async partial Task<IActionResult> ReconcileAsync(CancellationToken cancellationToken)
    {
        NetworkInterfaceInventoryResponse response = await inventory.ReconcileAsync(cancellationToken);
        return Ok(response);
    }

    public async partial Task<IActionResult> UpdateCommentAsync(Guid id, UpdateNetworkInterfaceCommentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        NetworkInterfaceInventoryResponse? response = await inventory.UpdateCommentAsync(id, request.Comment, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    public async partial Task<IActionResult> UpdateVisibilityAsync(Guid id, UpdateNetworkInterfaceVisibilityRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        NetworkInterfaceInventoryResponse? response = await inventory.UpdateVisibilityAsync(id, request.IsVisible, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }
}
