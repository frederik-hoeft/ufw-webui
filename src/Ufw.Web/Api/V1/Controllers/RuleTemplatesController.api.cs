using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Model.V1.Errors;
using Ufw.Web.Model.V1.RuleTemplates;

namespace Ufw.Web.Api.V1.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/rule-templates")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class RuleTemplatesController
{
    /// <summary>
    /// Returns the application-global rule-template catalog.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<RuleTemplateInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public partial Task<ActionResult<RuleTemplateInventoryResponse>> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates a reusable rule template without mutating firewall state.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<RuleTemplateInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public partial Task<IActionResult> CreateAsync([FromBody] CreateRuleTemplateRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Updates reusable rule-template authoring state without mutating firewall state.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<RuleTemplateInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status404NotFound)]
    public partial Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateRuleTemplateRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a rule template without modifying firewall state.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType<RuleTemplateInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiProblemDetails>(StatusCodes.Status404NotFound)]
    public partial Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
