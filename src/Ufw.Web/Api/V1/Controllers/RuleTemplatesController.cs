using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Model.V1.RuleTemplates;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class RuleTemplatesController(IRuleTemplateService templates) : ControllerBase
{
    public async partial Task<ActionResult<RuleTemplateInventoryResponse>> GetAsync(CancellationToken cancellationToken)
    {
        RuleTemplateInventoryResponse response = await templates.GetAsync(cancellationToken);
        return Ok(response);
    }

    public async partial Task<IActionResult> CreateAsync(CreateRuleTemplateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuleTemplateMutationResult result = await templates.CreateAsync(request, cancellationToken);
        return MapMutation(result);
    }

    public async partial Task<IActionResult> UpdateAsync(Guid id, UpdateRuleTemplateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RuleTemplateMutationResult result = await templates.UpdateAsync(id, request, cancellationToken);
        return MapMutation(result);
    }

    public async partial Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        RuleTemplateMutationResult result = await templates.DeleteAsync(id, cancellationToken);
        return MapMutation(result);
    }

    private IActionResult MapMutation(RuleTemplateMutationResult result) => result.Outcome switch
    {
        RuleTemplateMutationOutcome.Success => Ok(result.Inventory),
        RuleTemplateMutationOutcome.NotFound => NotFound(),
        RuleTemplateMutationOutcome.NameConflict => Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Rule template name already exists",
            Detail = "Rule template names must be unique without regard to case.",
        }),
        RuleTemplateMutationOutcome.InvalidTemplate => BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Rule template is invalid",
            Detail = "The template name, description, rule definition, or metadata values are invalid.",
        }),
        RuleTemplateMutationOutcome.TagNotFound => BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Rule template tag does not exist",
            Detail = "One or more referenced rule tags no longer exist.",
        }),
        RuleTemplateMutationOutcome.GroupNotFound => BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Rule template group does not exist",
            Detail = "The referenced rule group no longer exists.",
        }),
        _ => throw new InvalidOperationException($"Unknown rule-template mutation outcome '{result.Outcome}'."),
    };
}
