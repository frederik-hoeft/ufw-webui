namespace Ufw.Client.Api;

/// <summary>
/// Browser-facing boundary for confirmed firewall rule ordering mutations.
/// </summary>
/// <remarks>
/// The production REST/signed-intent contract is intentionally not defined yet. The frontend currently
/// registers a mock implementation so ordering UX can be exercised without implying an approved backend protocol.
/// </remarks>
public interface IRuleOrderingApiClient
{
    bool UsesMockData { get; }

    Task ApplyAsync(RuleOrderingApplyRequest request, CancellationToken cancellationToken = default);
}
