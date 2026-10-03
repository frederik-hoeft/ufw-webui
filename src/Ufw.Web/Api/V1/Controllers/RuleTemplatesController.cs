using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using Ufw.Shared.Firewall;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Data.Access;
using Ufw.Web.Data.Access.Rules;
using Ufw.Web.Data.Access.Rules.Templates;
using Ufw.Web.Model.V1.RuleTemplates;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Api.V1.Controllers;

public sealed partial class RuleTemplatesController(IRuleTemplateDataAccess templates) : ControllerBase
{
    public async partial Task<ActionResult<RuleTemplateInventoryResponse>> GetAsync(CancellationToken cancellationToken)
    {
        RuleTemplateInventoryResponse inventory = await GetInventoryAsync(cancellationToken);
        return Ok(inventory);
    }

    public async partial Task<IActionResult> CreateAsync(CreateRuleTemplateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryMapRequest(request, out RuleTemplateValues? values))
        {
            return InvalidTemplate();
        }

        DataMutationResult result = await templates.CreateAsync(values, cancellationToken);
        return await MapMutationAsync(result, cancellationToken);
    }

    public async partial Task<IActionResult> UpdateAsync(Guid id, UpdateRuleTemplateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryMapRequest(request, out RuleTemplateValues? values))
        {
            return InvalidTemplate();
        }

        DataMutationResult result = await templates.UpdateAsync(id, values, cancellationToken);
        return await MapMutationAsync(result, cancellationToken);
    }

    public async partial Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        DataMutationResult result = await templates.DeleteAsync(id, cancellationToken);
        return await MapMutationAsync(result, cancellationToken);
    }

    private async Task<IActionResult> MapMutationAsync(DataMutationResult result, CancellationToken cancellationToken)
    {
        if (result.IsSuccess)
        {
            RuleTemplateInventoryResponse inventory = await GetInventoryAsync(cancellationToken);
            return Ok(inventory);
        }

        return result.Error switch
        {
            DataMutationNotFoundError => NotFound(),
            RuleTagsNotFoundError => BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Rule template tag does not exist",
                Detail = "One or more referenced rule tags no longer exist.",
            }),
            RuleGroupNotFoundError => BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Rule template group does not exist",
                Detail = "The referenced rule group no longer exists.",
            }),
            DataMutationReferenceConflictError => BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Rule template dependency changed",
                Detail = "A referenced rule tag or group no longer exists.",
            }),
            _ => throw new InvalidOperationException($"Unexpected rule-template mutation error '{result.Error!.GetType().Name}'."),
        };
    }

    private async Task<RuleTemplateInventoryResponse> GetInventoryAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<RuleTemplateItem> items = await templates.GetAsync(cancellationToken);
        return new RuleTemplateInventoryResponse(items);
    }

    private static bool TryMapRequest(RuleTemplateRequest request, [NotNullWhen(true)] out RuleTemplateValues? values)
    {
        FirewallRuleSpecification rule = RuleSpecificationNormalizer.Normalize(request.Rule);
        if (RuleSpecificationValidator.Validate(rule).Length != 0)
        {
            values = null;
            return false;
        }

        RuleMetadataValues metadata = RuleMetadataValues.FromValidatedRequest(request.Notes, request.TagIds, request.GroupId);
        values = new RuleTemplateValues(
            request.Name.Trim(),
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            rule,
            metadata.Notes,
            metadata.TagIds,
            metadata.GroupId);
        return true;
    }

    private BadRequestObjectResult InvalidTemplate() => BadRequest(new ProblemDetails
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Rule template is invalid",
        Detail = "The template rule definition is invalid.",
    });
}
