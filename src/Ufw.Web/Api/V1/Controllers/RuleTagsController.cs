using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Api.V1.Models.Rules;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class RuleTagsController(IRuleTagService tags) : ControllerBase
{
    public async partial Task<ActionResult<RuleTagInventoryResponse>> GetAsync(CancellationToken cancellationToken)
    {
        RuleTagInventoryResponse response = await tags.GetAsync(cancellationToken);
        return Ok(response);
    }

    public async partial Task<IActionResult> CreateAsync(CreateRuleTagRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuleTagMutationResult result = await tags.CreateAsync(request, cancellationToken);
        return MapMutation(result);
    }

    public async partial Task<IActionResult> UpdateAsync(Guid id, UpdateRuleTagRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuleTagMutationResult result = await tags.UpdateAsync(id, request, cancellationToken);
        return MapMutation(result);
    }

    public async partial Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        RuleTagMutationResult result = await tags.DeleteAsync(id, cancellationToken);
        return MapMutation(result);
    }

    private IActionResult MapMutation(RuleTagMutationResult result) => result.Outcome switch
    {
        RuleTagMutationOutcome.Success => Ok(result.Inventory),
        RuleTagMutationOutcome.NotFound => NotFound(),
        RuleTagMutationOutcome.NameConflict => Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Rule tag name already exists",
            Detail = "Rule tag names must be unique without regard to case.",
        }),
        RuleTagMutationOutcome.InUse => Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Rule tag is still in use",
            Detail = "Remove the tag from all rule metadata before deleting it.",
        }),
        RuleTagMutationOutcome.InvalidTag => BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Rule tag is invalid",
            Detail = "Rule tag names must be non-empty and colors must use #RRGGBB syntax.",
        }),
        _ => throw new InvalidOperationException($"Unknown rule-tag mutation outcome '{result.Outcome}'."),
    };
}
