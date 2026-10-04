using Microsoft.AspNetCore.Mvc;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Api.V1.Errors;
using Ufw.Web.Data.Access;
using Ufw.Web.Data.Access.Rules.Tags;
using Ufw.Web.Model.V1.RuleTags;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class RuleTagsController(IRuleTagDataAccess tags) : ControllerBase
{
    public async partial Task<ActionResult<RuleTagInventoryResponse>> GetAsync(CancellationToken cancellationToken)
    {
        RuleTagInventoryResponse inventory = await GetInventoryAsync(cancellationToken);
        return Ok(inventory);
    }

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
            RuleTagInventoryResponse inventory = await GetInventoryAsync(cancellationToken);
            return Ok(inventory);
        }

        return result.Error switch
        {
            DataMutationNotFoundError => NotFound(ApiProblemDetailsFactory.Create(
                StatusCodes.Status404NotFound,
                title: "Rule tag not found",
                detail: "The requested rule tag does not exist.")),
            DataMutationReferenceConflictError => Conflict(ApiProblemDetailsFactory.Create(
                StatusCodes.Status409Conflict,
                title: "Rule tag is still in use",
                detail: "Remove the tag from live rule metadata and rule templates before deleting it.")),
            _ => throw new InvalidOperationException($"Unexpected rule-tag deletion error '{result.Error!.GetType().Name}'."),
        };
    }

    private async Task<IActionResult> MapUpsertMutationAsync(DataMutationResult result, CancellationToken cancellationToken)
    {
        if (result.IsSuccess)
        {
            RuleTagInventoryResponse inventory = await GetInventoryAsync(cancellationToken);
            return Ok(inventory);
        }

        return result.Error switch
        {
            DataMutationNotFoundError => NotFound(ApiProblemDetailsFactory.Create(
                StatusCodes.Status404NotFound,
                title: "Rule tag not found",
                detail: "The requested rule tag does not exist.")),
            DataMutationUniqueConflictError => Conflict(ApiProblemDetailsFactory.Create(
                StatusCodes.Status409Conflict,
                title: "Rule tag name already exists",
                detail: "Rule tag names must be unique without regard to case.")),
            _ => throw new InvalidOperationException($"Unexpected rule-tag mutation error '{result.Error!.GetType().Name}'."),
        };
    }

    private async Task<RuleTagInventoryResponse> GetInventoryAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<RuleTagItem> items = await tags.GetAsync(cancellationToken);
        return new RuleTagInventoryResponse(items);
    }
}
