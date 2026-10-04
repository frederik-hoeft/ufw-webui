using Microsoft.AspNetCore.Mvc;
using Ufw.Shared.Management.NetworkInterfaces;
using Ufw.Web.Api.V1.Errors;
using Ufw.Web.Model.V1.NetworkInterfaces;
using Ufw.Web.Services.NetworkInterfaces;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class NetworkInterfacesController(INetworkInterfaceInventoryService inventory) : ControllerBase
{
    public async partial Task<ActionResult<NetworkInterfaceInventoryResponse>> GetAsync(CancellationToken cancellationToken)
    {
        NetworkInterfaceInventorySnapshot snapshot = await inventory.GetCachedAsync(cancellationToken);
        return Ok(ToResponse(snapshot));
    }

    public async partial Task<IActionResult> ReconcileAsync(CancellationToken cancellationToken)
    {
        NetworkInterfaceInventorySnapshot snapshot = await inventory.ReconcileAsync(cancellationToken);
        return Ok(ToResponse(snapshot));
    }

    public async partial Task<IActionResult> UpdateCommentAsync(Guid id, UpdateNetworkInterfaceCommentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        NetworkInterfaceInventorySnapshot? snapshot = await inventory.UpdateCommentAsync(id, request.Comment, cancellationToken);
        return snapshot is null
            ? NotFound(ApiProblemDetailsFactory.Create(StatusCodes.Status404NotFound, title: "Network interface not found", detail: "The requested network interface does not exist."))
            : Ok(ToResponse(snapshot));
    }

    public async partial Task<IActionResult> UpdateVisibilityAsync(Guid id, UpdateNetworkInterfaceVisibilityRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        NetworkInterfaceInventorySnapshot? snapshot = await inventory.UpdateVisibilityAsync(id, request.IsVisible, cancellationToken);
        return snapshot is null
            ? NotFound(ApiProblemDetailsFactory.Create(StatusCodes.Status404NotFound, title: "Network interface not found", detail: "The requested network interface does not exist."))
            : Ok(ToResponse(snapshot));
    }

    public async partial Task<ActionResult<NetworkInterfaceCleanupResponse>> GetStaleAsync(CancellationToken cancellationToken)
    {
        NetworkInterfaceCleanupResult result = await inventory.GetStaleAsync(cancellationToken);
        return Ok(ToResponse(result));
    }

    public async partial Task<ActionResult<NetworkInterfaceCleanupResponse>> CleanupStaleAsync(CleanupNetworkInterfacesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        NetworkInterfaceCleanupResult result = await inventory.CleanupStaleAsync(request.InterfaceIds, cancellationToken);
        return Ok(ToResponse(result));
    }

    private static NetworkInterfaceInventoryResponse ToResponse(NetworkInterfaceInventorySnapshot snapshot) =>
        new(snapshot.Interfaces, snapshot.ReconciledAt);

    private static NetworkInterfaceCleanupResponse ToResponse(NetworkInterfaceCleanupResult result) =>
        new(result.StaleInterfaces, result.ReconciledAt, result.RemovedCount);
}
