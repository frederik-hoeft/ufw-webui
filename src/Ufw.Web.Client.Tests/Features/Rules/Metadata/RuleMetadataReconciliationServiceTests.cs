using Moq;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Api.RuleMetadata;
using Ufw.Web.Client.Api.RuleMetadata.Model;
using Ufw.Web.Client.Api.RuleTags.Model;
using Ufw.Web.Client.Api.Rules.Model;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Tests.Features.Rules.Metadata;

[TestClass]
public sealed class RuleMetadataReconciliationServiceTests
{
    [TestMethod]
    public async Task RefreshAsync_NormalizesAndOrdersOrphansAsync()
    {
        Guid alphaMetadataId = Guid.CreateVersion7();
        Guid zetaMetadataId = Guid.CreateVersion7();
        Guid tagId = Guid.CreateVersion7();
        Mock<IRuleMetadataReconciliationApiClient> api = new();
        api.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RuleMetadataReconciliationResponse
        {
            Orphans =
            [
                new RuleMetadataItem { Id = zetaMetadataId, RuleId = "zeta", Notes = "  note  " },
                new RuleMetadataItem
                {
                    Id = alphaMetadataId,
                    RuleId = "alpha",
                    Tags = [new RuleTagItem { Id = tagId, Name = " prod ", Color = "#aabbcc" }],
                },
            ],
        });
        RuleMetadataReconciliationService service = new(api.Object);

        RuleMetadataReconciliationSnapshot snapshot = await service.RefreshAsync();

        Assert.HasCount(2, snapshot.Orphans);
        Assert.AreEqual("alpha", snapshot.Orphans[0].RuleId);
        Assert.AreEqual(alphaMetadataId, snapshot.Orphans[0].Metadata.Id);
        Assert.AreEqual("prod", snapshot.Orphans[0].Metadata.Tags.Single().Name);
        Assert.AreEqual("#AABBCC", snapshot.Orphans[0].Metadata.Tags.Single().Color);
        Assert.AreEqual("note", snapshot.Orphans[1].Metadata.Notes);
        Assert.AreEqual(0, snapshot.RemovedCount);
    }

    [TestMethod]
    public async Task CleanupAsync_DeduplicatesSelectionAndPreservesServerRemovedCountAsync()
    {
        Guid metadataId = Guid.CreateVersion7();
        CleanupRuleMetadataRequest? captured = null;
        Mock<IRuleMetadataReconciliationApiClient> api = new();
        api.Setup(client => client.CleanupAsync(It.IsAny<CleanupRuleMetadataRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CleanupRuleMetadataRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new RuleMetadataReconciliationResponse { RemovedCount = 1 });
        RuleMetadataReconciliationService service = new(api.Object);

        RuleMetadataReconciliationSnapshot snapshot = await service.CleanupAsync([metadataId, metadataId]);

        Assert.IsNotNull(captured);
        CollectionAssert.AreEqual(new[] { metadataId }, captured.MetadataIds.ToArray());
        Assert.AreEqual(1, snapshot.RemovedCount);
        Assert.IsEmpty(snapshot.Orphans);
    }

    [TestMethod]
    public async Task CleanupAsync_InvalidSelectionRejectsBeforeCallingApiAsync()
    {
        Mock<IRuleMetadataReconciliationApiClient> api = new(MockBehavior.Strict);
        RuleMetadataReconciliationService service = new(api.Object);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.CleanupAsync([]));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.CleanupAsync([Guid.Empty]));
        api.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RefreshAsync_InvalidOrDuplicateResponse_ThrowsProtocolErrorAsync()
    {
        Guid metadataId = Guid.CreateVersion7();
        Mock<IRuleMetadataReconciliationApiClient> api = new();
        api.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RuleMetadataReconciliationResponse
        {
            Orphans =
            [
                new RuleMetadataItem { Id = metadataId, RuleId = "same" },
                new RuleMetadataItem { Id = metadataId, RuleId = "other" },
            ],
        });
        RuleMetadataReconciliationService service = new(api.Object);

        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => service.RefreshAsync());
    }
}
