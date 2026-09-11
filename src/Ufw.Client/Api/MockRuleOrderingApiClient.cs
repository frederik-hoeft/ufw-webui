namespace Ufw.Client.Api;

internal sealed class MockRuleOrderingApiClient : IRuleOrderingApiClient
{
    public bool UsesMockData => true;

    public Task ApplyAsync(RuleOrderingApplyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Moves.Count == 0)
        {
            throw new ArgumentException("At least one staged rule move is required.", nameof(request));
        }

        foreach (RuleMoveRequest move in request.Moves)
        {
            if (string.IsNullOrWhiteSpace(move.RuleId))
            {
                throw new ArgumentException("A rule ID is required for ordering.", nameof(request));
            }

            if (move.TargetPosition < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "The target position must be positive.");
            }
        }

        return Task.CompletedTask;
    }
}
