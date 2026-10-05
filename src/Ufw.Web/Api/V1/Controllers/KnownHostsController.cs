using Microsoft.AspNetCore.Mvc;
using Ufw.Shared.Management.KnownHosts;
using Ufw.Web.Api.V1.Errors;
using Ufw.Web.Data.Access;
using Ufw.Web.Data.Access.KnownHosts;
using Ufw.Web.Model.V1.KnownHosts;
using Ufw.Web.Services.KnownHosts;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class KnownHostsController(IKnownHostService knownHosts) : ControllerBase
{
    public async partial Task<ActionResult<KnownHostInventoryResponse>> GetAsync(CancellationToken cancellationToken)
    {
        KnownHostInventoryResponse response = await GetInventoryAsync(cancellationToken);
        return Ok(response);
    }

    public async partial Task<IActionResult> CreateAsync(CreateKnownHostRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> result = await knownHosts.CreateAsync(request, cancellationToken);
        return MapMutation(result);
    }

    public async partial Task<IActionResult> UpdateAsync(Guid id, UpdateKnownHostRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> result = await knownHosts.UpdateAsync(id, request, cancellationToken);
        return MapMutation(result);
    }

    public async partial Task<IActionResult> ReconcileDnsAsync(Guid id, CancellationToken cancellationToken)
    {
        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> result = await knownHosts.ReconcileDnsAsync(id, cancellationToken);
        return MapMutation(result);
    }

    public async partial Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> result = await knownHosts.DeleteAsync(id, cancellationToken);
        return MapMutation(result);
    }

    private IActionResult MapMutation(DataMutationResult<IReadOnlyList<KnownHostInventoryItem>> result)
    {
        if (result.IsSuccess)
        {
            return Ok(new KnownHostInventoryResponse(result.Value));
        }

        return result.Error switch
        {
            DataMutationNotFoundError => NotFound(ApiProblemDetailsFactory.Create(
                StatusCodes.Status404NotFound,
                title: "Known host not found",
                detail: "The requested known host does not exist.")),
            DataMutationUniqueConflictError => Conflict(ApiProblemDetailsFactory.Create(
                StatusCodes.Status409Conflict,
                title: "Known host name already exists",
                detail: "Known host names must be unique.")),
            KnownHostAddressFamilyConflictError => Conflict(ApiProblemDetailsFactory.Create(
                StatusCodes.Status409Conflict,
                title: "Known host address family cannot be changed",
                detail: "Create a new known host to replace an IPv4 alias with IPv6 or vice versa.")),
            KnownHostDnsResolutionFailedError => UnprocessableEntity(ApiProblemDetailsFactory.Create(
                StatusCodes.Status422UnprocessableEntity,
                title: "Known host DNS resolution failed",
                detail: "The alias name did not resolve to an address in the configured address family.")),
            KnownHostNotDnsManagedError => Conflict(ApiProblemDetailsFactory.Create(
                StatusCodes.Status409Conflict,
                title: "Known host is not DNS-backed",
                detail: "Only DNS-backed known hosts can be reconciled from DNS.")),
            KnownHostDnsConfigurationChangedError => Conflict(ApiProblemDetailsFactory.Create(
                StatusCodes.Status409Conflict,
                title: "Known host DNS configuration changed",
                detail: "The known host changed while DNS was being resolved. Retry the reconciliation against the current configuration.")),
            _ => throw new InvalidOperationException($"Unexpected known-host mutation error '{result.Error!.GetType().Name}'."),
        };
    }

    private async Task<KnownHostInventoryResponse> GetInventoryAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<KnownHostInventoryItem> hosts = await knownHosts.GetAsync(cancellationToken);
        return new KnownHostInventoryResponse(hosts);
    }
}
