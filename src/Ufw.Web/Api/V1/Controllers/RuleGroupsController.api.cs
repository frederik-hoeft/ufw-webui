using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Model.V1.RuleGroups;

namespace Ufw.Web.Api.V1.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/rule-groups")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class RuleGroupsController
{
    /// <summary>
    /// Returns the ASP-owned rule-group catalog and semantic member identities.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<RuleGroupInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public partial Task<ActionResult<RuleGroupInventoryResponse>> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates a reusable rule group. Empty groups are supported.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<RuleGroupInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public partial Task<IActionResult> CreateAsync([FromBody] CreateRuleGroupRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the display name and comment of a rule group.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<RuleGroupInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public partial Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateRuleGroupRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an empty rule group without modifying firewall rules.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType<RuleGroupInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public partial Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
