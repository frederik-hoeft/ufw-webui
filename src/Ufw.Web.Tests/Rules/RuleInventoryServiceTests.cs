using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Tests.Rules;

[TestClass]
public sealed class RuleInventoryServiceTests
{
    [TestMethod]
    public async Task GetAsync_StampsEnrichedSnapshotWithCaptureTimeAsync()
    {
        DateTimeOffset capturedAt = new(2026, 9, 25, 15, 30, 0, TimeSpan.Zero);
        RuleListResponse firewall = new(Active: true, [], TestFirewallConfiguration.Enabled);
        TestDaemonRuleSource daemon = new(firewall);
        TestRuleMetadataRepository metadata = new();
        RuleInventoryService service = new(daemon, metadata, new TestTimeProvider(capturedAt));

        RuleInventoryResponse response = await service.GetAsync();

        Assert.AreSame(firewall, response.Firewall);
        Assert.AreEqual(capturedAt, response.CapturedAt);
        Assert.IsEmpty(metadata.LastRuleIds);
    }

    private sealed class TestDaemonRuleSource(RuleListResponse response) : IDaemonRuleSource
    {
        public Task<RuleListResponse> GetAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(response);
        }
    }

    private sealed class TestRuleMetadataRepository : IRuleMetadataRepository
    {
        public IReadOnlyCollection<string> LastRuleIds { get; private set; } = [];

        public Task<IReadOnlyList<RuleMetadataItem>> GetForRuleIdsAsync(IReadOnlyCollection<string> ruleIds, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRuleIds = ruleIds;
            return Task.FromResult<IReadOnlyList<RuleMetadataItem>>([]);
        }

        public Task<IReadOnlyList<RuleMetadataItem>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RuleMetadataSaveResult> SaveAsync(string ruleId, RuleMetadataValues values, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> DeleteAsync(string ruleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> DeleteForRuleIdsAsync(IReadOnlyCollection<string> ruleIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> DeleteUnmatchedAsync(IReadOnlyCollection<Guid> metadataIds, IReadOnlyCollection<string> liveRuleIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
