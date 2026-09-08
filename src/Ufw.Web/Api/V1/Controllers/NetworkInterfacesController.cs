using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ufw.Ipc.Client;
using Ufw.Web.Api.V1.Models.NetworkInterfaces;
using Ufw.Web.Data;
using Ufw.Web.Services.NetworkInterfaces;
using Wkg.AspNetCore.Abstractions.Controllers;
using Wkg.AspNetCore.Transactions;

namespace Ufw.Web.Api.V1.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/network-interfaces")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class NetworkInterfacesController(
    INetworkInterfaceInventoryService inventory,
    ITransactionServiceHandle transactionService) : DatabaseController<ApplicationDbContext>(transactionService)
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
    public Task<IActionResult> ReconcileAsync(CancellationToken cancellationToken) =>
        Transaction.Scoped.RunAsync<IActionResult>(async (_, transaction, ct) =>
        {
            try
            {
                NetworkInterfaceInventoryResponse response = await inventory.ReconcileAsync(ct);
                return transaction.Commit(Ok(response));
            }
            catch (UfwIpcException exception)
            {
                return transaction.Rollback(MapDaemonError(exception));
            }
            catch (InvalidDataException exception)
            {
                return transaction.Rollback(Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    detail: exception.Message));
            }
        }, cancellationToken);

    [HttpPut("{id:guid}/comment")]
    [ProducesResponseType<NetworkInterfaceInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> UpdateCommentAsync(
        Guid id,
        [FromBody] UpdateNetworkInterfaceCommentRequest request,
        CancellationToken cancellationToken) =>
        Transaction.Scoped.RunAsync<IActionResult>(async (_, transaction, ct) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            NetworkInterfaceInventoryResponse? response = await inventory.UpdateCommentAsync(id, request.Comment, ct);
            return response is null
                ? transaction.Rollback(NotFound())
                : transaction.Commit(Ok(response));
        }, cancellationToken);

    private ObjectResult MapDaemonError(UfwIpcException exception) => Problem(
        statusCode: StatusCodes.Status502BadGateway,
        detail: exception.ResponseMessage);
}
