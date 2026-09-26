using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Model.V1.RuleGroups;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class RuleGroupsController(IRuleGroupService groups) : ControllerBase
{
    public async partial Task<ActionResult<RuleGroupInventoryResponse>> GetAsync(CancellationToken cancellationToken)
    {
        RuleGroupInventoryResponse response = await groups.GetAsync(cancellationToken);
        return Ok(response);
    }

    public async partial Task<IActionResult> CreateAsync(CreateRuleGroupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuleGroupMutationResult result = await groups.CreateAsync(request, cancellationToken);
        return MapMutation(result);
    }

    public async partial Task<IActionResult> UpdateAsync(Guid id, UpdateRuleGroupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuleGroupMutationResult result = await groups.UpdateAsync(id, request, cancellationToken);
        return MapMutation(result);
    }

    public async partial Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        RuleGroupMutationResult result = await groups.DeleteAsync(id, cancellationToken);
        return MapMutation(result);
    }

    private IActionResult MapMutation(RuleGroupMutationResult result) => result.Outcome switch
    {
        RuleGroupMutationOutcome.Success => Ok(result.Inventory),
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
            Detail = "Remove or reconcile all rule metadata memberships before deleting the group.",
        }),
        RuleGroupMutationOutcome.InvalidGroup => BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Rule group is invalid",
            Detail = "Rule group names must be non-empty and the name/comment lengths must be within the supported limits.",
        }),
        _ => throw new InvalidOperationException($"Unknown rule-group mutation outcome '{result.Outcome}'."),
    };
}
