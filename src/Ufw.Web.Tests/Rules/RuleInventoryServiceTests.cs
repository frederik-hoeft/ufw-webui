using Ufw.Shared.Ipc.Model.Requests.Domain;
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
        TestRuleDaemonGateway daemon = new(firewall);
        TestRuleMetadataRepository metadata = new();
        RuleInventoryService service = new(daemon, metadata, new TestTimeProvider(capturedAt));

        RuleInventoryResponse response = await service.GetAsync();

        Assert.AreSame(firewall, response.Firewall);
        Assert.AreEqual(capturedAt, response.CapturedAt);
        Assert.IsEmpty(metadata.LastRuleIds);
    }

    private sealed class TestRuleDaemonGateway(RuleListResponse response) : IRuleDaemonGateway
    {
        public Task<RuleListResponse> GetRulesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(response);
        }

        public Task<RuleMutationResponse> AddRuleAsync(AddRuleRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RuleInsertionResponse> InsertRuleAsync(InsertRuleRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RuleReplacementResponse> ReplaceRuleAsync(ReplaceRuleRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RuleReorderResponse> ReorderRulesAsync(ReorderRulesRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RuleBatchDeleteResponse> BatchDeleteRulesAsync(BatchDeleteRulesRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RuleMutationResponse> DeleteRuleAsync(DeleteRuleRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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

        public Task<RuleMetadataReplacementPersistenceOutcome> ReconcileReplacementAsync(string originalRuleId, string replacementRuleId, bool originalRuleStillLive, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

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
