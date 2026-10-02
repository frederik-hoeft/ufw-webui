using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Data.Access.Rules.Groups;
using Ufw.Web.Model.V1.RuleGroups;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class RuleGroupsController(IRuleGroupDataAccess groups) : ControllerBase
{
    public async partial Task<ActionResult<RuleGroupInventoryResponse>> GetAsync(CancellationToken cancellationToken)
    {
        return Ok(new RuleGroupInventoryResponse(await groups.GetAsync(cancellationToken)));
    }

    public async partial Task<IActionResult> CreateAsync(CreateRuleGroupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuleGroupMutationResult result = await groups.CreateAsync(request.Name.Trim(), NormalizeOptional(request.Comment), cancellationToken);
        return await MapMutationAsync(result, cancellationToken);
    }

    public async partial Task<IActionResult> UpdateAsync(Guid id, UpdateRuleGroupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuleGroupMutationResult result = await groups.UpdateAsync(id, request.Name.Trim(), NormalizeOptional(request.Comment), cancellationToken);
        return await MapMutationAsync(result, cancellationToken);
    }

    public async partial Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        RuleGroupMutationResult result = await groups.DeleteAsync(id, cancellationToken);
        return await MapMutationAsync(result, cancellationToken);
    }

    private async Task<IActionResult> MapMutationAsync(RuleGroupMutationResult result, CancellationToken cancellationToken)
    {
        if (result.Outcome is RuleGroupMutationOutcome.Success)
        {
            return Ok(new RuleGroupInventoryResponse(await groups.GetAsync(cancellationToken)));
        }

        return result.Outcome switch
        {
            RuleGroupMutationOutcome.NotFound => NotFound(),
            RuleGroupMutationOutcome.NameConflict => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Rule group name already exists",
                Detail = "Rule group names must be unique without regard to case.",
            }),
            RuleGroupMutationOutcome.InUse => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Rule group is still in use",
                Detail = "Remove all live rule metadata and rule-template references before deleting the group.",
            }),
            _ => throw new InvalidOperationException($"Unknown rule-group mutation outcome '{result.Outcome}'."),
        };
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
