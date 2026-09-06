namespace Ufw.Client.Api;

internal sealed class MockRuleOrderingApiClient : IRuleOrderingApiClient
{
    public bool UsesMockData => true;

    public Task MoveAsync(RuleMoveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.RuleId))
        {
            throw new ArgumentException("A rule ID is required for ordering.", nameof(request));
        }

        if (request.TargetPosition < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The target position must be positive.");
        }

        return Task.CompletedTask;
    }
}
