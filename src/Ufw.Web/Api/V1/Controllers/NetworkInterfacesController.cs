using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ufw.Ipc.Client;
using Ufw.Web.Api.V1.Models.NetworkInterfaces;
using Ufw.Web.Services.NetworkInterfaces;

namespace Ufw.Web.Api.V1.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/network-interfaces")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class NetworkInterfacesController(INetworkInterfaceInventoryService inventory) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<NetworkInterfaceInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<NetworkInterfaceInventoryResponse>> GetAsync(CancellationToken cancellationToken)
    {
        NetworkInterfaceInventoryResponse response = await inventory.GetCachedAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPost("reconcile")]
    [ProducesResponseType<NetworkInterfaceInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ReconcileAsync(CancellationToken cancellationToken)
    {
        try
        {
            NetworkInterfaceInventoryResponse response = await inventory.ReconcileAsync(cancellationToken);
            return Ok(response);
        }
        catch (UfwIpcException exception)
        {
            return MapDaemonError(exception);
        }
        catch (InvalidDataException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                detail: exception.Message);
        }
    }

    [HttpPut("{id:guid}/comment")]
    [ProducesResponseType<NetworkInterfaceInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCommentAsync(
        Guid id,
        [FromBody] UpdateNetworkInterfaceCommentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        NetworkInterfaceInventoryResponse? response = await inventory.UpdateCommentAsync(id, request.Comment, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPut("{id:guid}/visibility")]
    [ProducesResponseType<NetworkInterfaceInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateVisibilityAsync(
        Guid id,
        [FromBody] UpdateNetworkInterfaceVisibilityRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        NetworkInterfaceInventoryResponse? response = await inventory.UpdateVisibilityAsync(
            id,
            request.IsVisible,
            cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    private ObjectResult MapDaemonError(UfwIpcException exception) => Problem(
        statusCode: StatusCodes.Status502BadGateway,
        detail: exception.ResponseMessage);
}
