using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ufw.Web.Api.V1.Models.KnownHosts;

namespace Ufw.Web.Api.V1.Controllers;

[Authorize]
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/known-hosts")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class KnownHostsController
{
    /// <summary>
    /// Returns the ASP-owned known-host catalog used for rule-authoring suggestions.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<KnownHostInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public partial Task<ActionResult<KnownHostInventoryResponse>> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates a known-host alias for a canonical literal IPv4/IPv6 host or network address.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<KnownHostInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public partial Task<IActionResult> CreateAsync([FromBody] CreateKnownHostRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Updates a known-host alias without allowing its address family to change.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<KnownHostInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public partial Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateKnownHostRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an authoring alias without modifying firewall rules that already contain its resolved literal address.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType<KnownHostInventoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public partial Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
