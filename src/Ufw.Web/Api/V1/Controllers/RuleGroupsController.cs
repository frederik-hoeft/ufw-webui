using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Data.Access;
using Ufw.Web.Data.Access.Rules.Groups;
using Ufw.Web.Model.V1.RuleGroups;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class RuleGroupsController(IRuleGroupDataAccess groups) : ControllerBase
{
    public async partial Task<ActionResult<RuleGroupInventoryResponse>> GetAsync(CancellationToken cancellationToken) =>
        Ok(await GetInventoryAsync(cancellationToken));

    public async partial Task<IActionResult> CreateAsync(CreateRuleGroupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        DataMutationResult result = await groups.CreateAsync(request.Name.Trim(), NormalizeOptional(request.Comment), cancellationToken);
        return await MapUpsertMutationAsync(result, cancellationToken);
    }

    public async partial Task<IActionResult> UpdateAsync(Guid id, UpdateRuleGroupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        DataMutationResult result = await groups.UpdateAsync(id, request.Name.Trim(), NormalizeOptional(request.Comment), cancellationToken);
        return await MapUpsertMutationAsync(result, cancellationToken);
    }

    public async partial Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        DataMutationResult result = await groups.DeleteAsync(id, cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(await GetInventoryAsync(cancellationToken));
        }

        return result.Error switch
        {
            DataMutationNotFoundError => NotFound(),
            DataMutationReferenceConflictError => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Rule group is still in use",
                Detail = "Remove all live rule metadata and rule-template references before deleting the group.",
            }),
            _ => throw new InvalidOperationException($"Unexpected rule-group deletion error '{result.Error!.GetType().Name}'."),
        };
    }

    private async Task<IActionResult> MapUpsertMutationAsync(DataMutationResult result, CancellationToken cancellationToken)
    {
        if (result.IsSuccess)
        {
            return Ok(await GetInventoryAsync(cancellationToken));
        }

        return result.Error switch
        {
            DataMutationNotFoundError => NotFound(),
            DataMutationUniqueConflictError => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Rule group name already exists",
                Detail = "Rule group names must be unique without regard to case.",
            }),
            _ => throw new InvalidOperationException($"Unexpected rule-group mutation error '{result.Error!.GetType().Name}'."),
        };
    }

    private async Task<RuleGroupInventoryResponse> GetInventoryAsync(CancellationToken cancellationToken) => new(await groups.GetAsync(cancellationToken));

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
