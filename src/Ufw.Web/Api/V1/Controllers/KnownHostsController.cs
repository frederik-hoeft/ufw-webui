using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Api.V1.Models.KnownHosts;
using Ufw.Web.Services.KnownHosts;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class KnownHostsController(IKnownHostService knownHosts) : ControllerBase
{
    public async partial Task<ActionResult<KnownHostInventoryResponse>> GetAsync(CancellationToken cancellationToken)
    {
        KnownHostInventoryResponse response = await knownHosts.GetAsync(cancellationToken);
        return Ok(response);
    }

    public async partial Task<IActionResult> CreateAsync(CreateKnownHostRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        KnownHostMutationResult result = await knownHosts.CreateAsync(request, cancellationToken);
        return MapMutation(result);
    }

    public async partial Task<IActionResult> UpdateAsync(Guid id, UpdateKnownHostRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        KnownHostMutationResult result = await knownHosts.UpdateAsync(id, request, cancellationToken);
        return MapMutation(result);
    }

    public async partial Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        KnownHostMutationResult result = await knownHosts.DeleteAsync(id, cancellationToken);
        return MapMutation(result);
    }

    private IActionResult MapMutation(KnownHostMutationResult result) => result.Outcome switch
    {
        KnownHostMutationOutcome.Success => Ok(result.Inventory),
        KnownHostMutationOutcome.NotFound => NotFound(),
        KnownHostMutationOutcome.NameConflict => Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Known host name already exists",
            Detail = "Known host names must be unique.",
        }),
        KnownHostMutationOutcome.AddressFamilyConflict => Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Known host address family cannot be changed",
            Detail = "Create a new known host to replace an IPv4 alias with IPv6 or vice versa.",
        }),
        KnownHostMutationOutcome.InvalidAddress => BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Known host address is invalid",
            Detail = "The address must be a literal IPv4 or IPv6 host or CIDR network.",
        }),
        _ => throw new InvalidOperationException($"Unknown known-host mutation outcome '{result.Outcome}'."),
    };
}
