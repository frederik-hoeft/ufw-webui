using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Api.V1.Models.NetworkInterfaces;

namespace Ufw.Web.Api.V1.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/network-interfaces")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class NetworkInterfacesController
{
    /// <summary>
    /// Returns the current cached network-interface inventory and application metadata.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<NetworkInterfaceInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public partial Task<ActionResult<NetworkInterfaceInventoryResponse>> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reconciles the cached inventory with the daemon-authoritative host interface list.
    /// </summary>
    [HttpPost("reconcile")]
    [ProducesResponseType<NetworkInterfaceInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public partial Task<IActionResult> ReconcileAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Updates the application-owned comment associated with a known network interface.
    /// </summary>
    [HttpPut("{id:guid}/comment")]
    [ProducesResponseType<NetworkInterfaceInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public partial Task<IActionResult> UpdateCommentAsync(Guid id, [FromBody] UpdateNetworkInterfaceCommentRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Updates whether a known network interface is shown in browser authoring suggestions.
    /// </summary>
    [HttpPut("{id:guid}/visibility")]
    [ProducesResponseType<NetworkInterfaceInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public partial Task<IActionResult> UpdateVisibilityAsync(Guid id, [FromBody] UpdateNetworkInterfaceVisibilityRequest request, CancellationToken cancellationToken);
}
