using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Model.V1.RuleTags;

namespace Ufw.Web.Api.V1.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/rule-tags")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class RuleTagsController
{
    /// <summary>
    /// Returns the ASP-owned rule-tag catalog.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<RuleTagInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public partial Task<ActionResult<RuleTagInventoryResponse>> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates a reusable rule tag.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<RuleTagInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public partial Task<IActionResult> CreateAsync([FromBody] CreateRuleTagRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the display name and color of a reusable rule tag.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<RuleTagInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public partial Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateRuleTagRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an unused rule tag.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType<RuleTagInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public partial Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
