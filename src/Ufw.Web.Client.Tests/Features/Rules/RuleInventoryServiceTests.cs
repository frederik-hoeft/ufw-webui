using Moq;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Api.Rules;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.Tests.Features.Rules;

[TestClass]
public sealed class RuleInventoryServiceTests
{
    [TestMethod]
    public async Task GetAsync_MapsAuthoritativeSnapshotAndPreservesAssessmentAsync()
    {
        using CancellationTokenSource lifetime = new();
        Mock<IRuleApiClient> api = new(MockBehavior.Strict);
        Guid metadataId = Guid.CreateVersion7();
        DateTimeOffset capturedAt = new(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);
        RuleListResponse firewall = new(true, [new ListedFirewallRule { RuleId = "live" }], TestFirewallConfiguration.Enabled)
        {
            Assessment = new FirewallStateAssessment([new FirewallStateIssue(FirewallStateAssessmentEvaluator.DUPLICATE_RULE_IDENTITY, "live", [0, 1])]),
        };
        api.Setup(client => client.GetInventoryAsync(lifetime.Token)).ReturnsAsync(new RuleInventoryResponse(firewall,
        [
            new RuleMetadataItem { Id = metadataId, RuleId = "live", Notes = " managed ", Tags = [] },
            new RuleMetadataItem { Id = Guid.CreateVersion7(), RuleId = "stale", Tags = [] },
        ], capturedAt));
        RuleInventoryService service = new(api.Object);

        RuleSnapshot snapshot = await service.GetAsync(lifetime.Token);

        Assert.AreEqual(capturedAt, snapshot.CapturedAt);
        Assert.AreSame(firewall.Assessment, snapshot.Assessment);
        Assert.AreEqual("managed", snapshot.Metadata["live"].Notes);
        Assert.AreEqual(metadataId, snapshot.Metadata["live"].Id);
        Assert.HasCount(1, snapshot.Metadata);
        api.VerifyAll();
    }

    [TestMethod]
    public async Task GetAsync_RejectsInvalidMetadataBeforeExposingSnapshotAsync()
    {
        Mock<IRuleApiClient> api = new();
        api.Setup(client => client.GetInventoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleInventoryResponse(new RuleListResponse(true, [], TestFirewallConfiguration.Enabled),
            [new RuleMetadataItem { Id = Guid.Empty, RuleId = "broken", Tags = [] }]));

        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => new RuleInventoryService(api.Object).GetAsync());
    }
}
