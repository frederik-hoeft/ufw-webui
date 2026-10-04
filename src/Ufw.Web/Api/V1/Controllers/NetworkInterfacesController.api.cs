using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Model.V1.Errors;
using Ufw.Web.Model.V1.NetworkInterfaces;

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
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status502BadGateway)]
    public partial Task<IActionResult> ReconcileAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Updates the application-owned comment associated with a known network interface.
    /// </summary>
    [HttpPut("{id:guid}/comment")]
    [ProducesResponseType<NetworkInterfaceInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status404NotFound)]
    public partial Task<IActionResult> UpdateCommentAsync(Guid id, [FromBody] UpdateNetworkInterfaceCommentRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Updates whether a known network interface is shown in browser authoring suggestions.
    /// </summary>
    [HttpPut("{id:guid}/visibility")]
    [ProducesResponseType<NetworkInterfaceInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status404NotFound)]
    public partial Task<IActionResult> UpdateVisibilityAsync(Guid id, [FromBody] UpdateNetworkInterfaceVisibilityRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Returns retained metadata for interfaces that were absent from the last daemon reconciliation.
    /// </summary>
    [HttpGet("stale")]
    [ProducesResponseType<NetworkInterfaceCleanupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public partial Task<ActionResult<NetworkInterfaceCleanupResponse>> GetStaleAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Revalidates interface presence against the daemon and permanently removes selected metadata that is still stale.
    /// </summary>
    [HttpPost("stale/cleanup")]
    [ProducesResponseType<NetworkInterfaceCleanupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status502BadGateway)]
    public partial Task<ActionResult<NetworkInterfaceCleanupResponse>> CleanupStaleAsync([FromBody] CleanupNetworkInterfacesRequest request, CancellationToken cancellationToken);
}
