using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Data.Access;
using Ufw.Web.Data.Access.Rules.Tags;
using Ufw.Web.Model.V1.RuleTags;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class RuleTagsController(IRuleTagDataAccess tags) : ControllerBase
{
    public async partial Task<ActionResult<RuleTagInventoryResponse>> GetAsync(CancellationToken cancellationToken) =>
        Ok(await GetInventoryAsync(cancellationToken));

    public async partial Task<IActionResult> CreateAsync(CreateRuleTagRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        DataMutationResult result = await tags.CreateAsync(request.Name.Trim(), request.Color.Trim().ToUpperInvariant(), cancellationToken);
        return await MapUpsertMutationAsync(result, cancellationToken);
    }

    public async partial Task<IActionResult> UpdateAsync(Guid id, UpdateRuleTagRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        DataMutationResult result = await tags.UpdateAsync(id, request.Name.Trim(), request.Color.Trim().ToUpperInvariant(), cancellationToken);
        return await MapUpsertMutationAsync(result, cancellationToken);
    }

    public async partial Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        DataMutationResult result = await tags.DeleteAsync(id, cancellationToken);
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
                Title = "Rule tag is still in use",
                Detail = "Remove the tag from live rule metadata and rule templates before deleting it.",
            }),
            _ => throw new InvalidOperationException($"Unexpected rule-tag deletion error '{result.Error!.GetType().Name}'."),
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
                Title = "Rule tag name already exists",
                Detail = "Rule tag names must be unique without regard to case.",
            }),
            _ => throw new InvalidOperationException($"Unexpected rule-tag mutation error '{result.Error!.GetType().Name}'."),
        };
    }

    private async Task<RuleTagInventoryResponse> GetInventoryAsync(CancellationToken cancellationToken) => new(await tags.GetAsync(cancellationToken));
}
