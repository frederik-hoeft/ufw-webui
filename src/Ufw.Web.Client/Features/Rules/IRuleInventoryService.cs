namespace Ufw.Web.Client.Features.Rules;

/// <summary>
/// Reads an authoritative rule inventory and maps it to the client-domain snapshot.
/// </summary>
internal interface IRuleInventoryService
{
    Task<RuleSnapshot> GetAsync(CancellationToken cancellationToken = default);
}
