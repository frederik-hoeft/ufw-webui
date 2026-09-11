using Ufw.Client.Api;

namespace Ufw.Client.Tests.Api;

[TestClass]
public sealed class MockRuleOrderingApiClientTests
{
    private readonly MockRuleOrderingApiClient _client = new();

    [TestMethod]
    public async Task ApplyAsync_AcceptsOneBasedMovesWithStableIdsAsync()
    {
        await _client.ApplyAsync(new RuleOrderingApplyRequest([new RuleMoveRequest("stable-id", 1), new RuleMoveRequest("other", 2)]));

        Assert.IsTrue(_client.UsesMockData);
    }

    [TestMethod]
    public async Task ApplyAsync_RejectsEmptyMoveSetMissingIdsAndNonPositivePositionsAsync()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => _client.ApplyAsync(new RuleOrderingApplyRequest([])));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => _client.ApplyAsync(new RuleOrderingApplyRequest([new RuleMoveRequest(" ", 1)])));
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => _client.ApplyAsync(new RuleOrderingApplyRequest([new RuleMoveRequest("id", 0)])));
    }

    [TestMethod]
    public async Task ApplyAsync_PreCanceledRequestHasNoEffectAsync()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _client.ApplyAsync(new RuleOrderingApplyRequest([new RuleMoveRequest("id", 1)]), cancellation.Token));
    }
}
