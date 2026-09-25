using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Model.V1.KnownHosts;
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

    public async partial Task<IActionResult> ReconcileDnsAsync(Guid id, CancellationToken cancellationToken)
    {
        KnownHostMutationResult result = await knownHosts.ReconcileDnsAsync(id, cancellationToken);
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
        KnownHostMutationOutcome.InvalidDnsConfiguration => BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Known host DNS configuration is invalid",
            Detail = "DNS-backed aliases require an IPv4 or IPv6 address family.",
        }),
        KnownHostMutationOutcome.DnsResolutionFailed => UnprocessableEntity(new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Known host DNS resolution failed",
            Detail = "The alias name did not resolve to an address in the configured address family.",
        }),
        KnownHostMutationOutcome.NotDnsManaged => Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Known host is not DNS-backed",
            Detail = "Only DNS-backed known hosts can be reconciled from DNS.",
        }),
        KnownHostMutationOutcome.DnsConfigurationChanged => Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Known host DNS configuration changed",
            Detail = "The known host changed while DNS was being resolved. Retry the reconciliation against the current configuration.",
        }),
        _ => throw new InvalidOperationException($"Unknown known-host mutation outcome '{result.Outcome}'."),
    };
}
