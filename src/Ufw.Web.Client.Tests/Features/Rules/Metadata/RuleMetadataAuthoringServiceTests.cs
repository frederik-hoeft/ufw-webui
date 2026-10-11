using Moq;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Tests.Features.Rules.Metadata;

[TestClass]
public sealed class RuleMetadataAuthoringServiceTests
{
    [TestMethod]
    public async Task RefreshAsync_RefreshesBothCatalogsAsync()
    {
        Mock<IRuleTagCatalogService> tags = new();
        Mock<IRuleGroupCatalogService> groups = new();
        tags.Setup(catalog => catalog.RefreshAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        groups.Setup(catalog => catalog.RefreshAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        RuleMetadataAuthoringService service = new(tags.Object, groups.Object, Mock.Of<IRuleTagColorGenerator>());

        await service.RefreshAsync();

        tags.Verify(catalog => catalog.RefreshAsync(It.IsAny<CancellationToken>()), Times.Once);
        groups.Verify(catalog => catalog.RefreshAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public void SearchTags_ExcludesSelectedAndUsesWholeCatalogForCreateName()
    {
        Guid selectedId = Guid.CreateVersion7();
        Guid candidateId = Guid.CreateVersion7();
        RuleTag selected = new(selectedId, "Production", "#112233");
        RuleTag candidate = new(candidateId, "Preprod", "#445566");
        RuleMetadataAuthoringService service = Create(tags: [selected, candidate]);

        MetadataCatalogSearchResult<RuleTag> matching = service.SearchTags("  pre  ", [selectedId]);
        Assert.HasCount(1, matching.Matches);
        Assert.AreEqual(candidateId, matching.Matches[0].Id);
        Assert.AreEqual("pre", matching.CreateName);

        MetadataCatalogSearchResult<RuleTag> exactSelected = service.SearchTags(" production ", [selectedId]);
        Assert.IsEmpty(exactSelected.Matches);
        Assert.IsNull(exactSelected.CreateName);

        Assert.IsNull(service.SearchTags(new string('a', RuleTagLimits.MAX_NAME_LENGTH + 1), []).CreateName);
        Assert.IsNull(service.SearchTags("   ", []).CreateName);
        CollectionAssert.AreEquivalent(new[] { selectedId }, service.SelectTags([selectedId, Guid.NewGuid()]).Select(static tag => tag.Id).ToArray());
    }

    [TestMethod]
    public void SearchGroups_UsesGroupLimitsAndResolvesCatalogMembership()
    {
        Guid id = Guid.CreateVersion7();
        RuleGroup group = Group(id, "Operations");
        RuleMetadataAuthoringService service = Create(groups: [group]);

        MetadataCatalogSearchResult<RuleGroup> result = service.SearchGroups("  oper  ");
        Assert.HasCount(1, result.Matches);
        Assert.AreEqual(id, result.Matches[0].Id);
        Assert.AreEqual("oper", result.CreateName);
        Assert.IsNull(service.SearchGroups("  OPERATIONS ").CreateName);
        Assert.IsNull(service.SearchGroups(new string('x', RuleGroupLimits.MAX_NAME_LENGTH + 1)).CreateName);
        Assert.AreSame(group, service.FindGroup(id));
        Assert.IsNull(service.FindGroup(Guid.CreateVersion7()));
    }

    [TestMethod]
    public void Search_CancellationDoesNotReturnStaleResults()
    {
        RuleMetadataAuthoringService service = Create();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(() => service.SearchTags("x", [], cts.Token));
        Assert.ThrowsExactly<OperationCanceledException>(() => service.SearchGroups("x", cts.Token));
    }

    [TestMethod]
    public async Task CreateTagAsync_UsesGeneratedColorAndSelectsServerResultAsync()
    {
        Guid id = Guid.CreateVersion7();
        RuleTag created = new(id, "Prod", "#123456");
        Mock<IRuleTagCatalogService> tags = new();
        Mock<IRuleTagColorGenerator> colors = new();
        colors.Setup(generator => generator.Generate()).Returns("#123456");
        tags.Setup(catalog => catalog.CreateAsync("Prod", "#123456", It.IsAny<CancellationToken>())).ReturnsAsync([created]);
        RuleMetadataAuthoringService service = new(tags.Object, Mock.Of<IRuleGroupCatalogService>(), colors.Object);

        RuleTag actual = await service.CreateTagAsync("  Prod  ");

        Assert.AreSame(created, actual);
        colors.Verify(generator => generator.Generate(), Times.Once);
    }

    [TestMethod]
    public async Task CreateGroupAsync_DoesNotInferSuccessFromMissingServerItemAsync()
    {
        Mock<IRuleGroupCatalogService> groups = new();
        groups.Setup(catalog => catalog.CreateAsync("Ops", null, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        RuleMetadataAuthoringService service = new(Mock.Of<IRuleTagCatalogService>(), groups.Object, Mock.Of<IRuleTagColorGenerator>());

        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => service.CreateGroupAsync(" Ops "));
    }

    [TestMethod]
    public async Task InvalidNames_DoNotCallCatalogMutationAsync()
    {
        Mock<IRuleTagCatalogService> tags = new();
        Mock<IRuleGroupCatalogService> groups = new();
        RuleMetadataAuthoringService service = new(tags.Object, groups.Object, Mock.Of<IRuleTagColorGenerator>());

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.CreateTagAsync("   "));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.CreateGroupAsync(new string('x', RuleGroupLimits.MAX_NAME_LENGTH + 1)));
        tags.Verify(catalog => catalog.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        groups.Verify(catalog => catalog.CreateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static RuleMetadataAuthoringService Create(IReadOnlyList<RuleTag>? tags = null, IReadOnlyList<RuleGroup>? groups = null)
    {
        Mock<IRuleTagCatalogService> tagCatalog = new();
        Mock<IRuleGroupCatalogService> groupCatalog = new();
        tagCatalog.SetupGet(catalog => catalog.Current).Returns(tags ?? []);
        groupCatalog.SetupGet(catalog => catalog.Current).Returns(groups ?? []);
        return new RuleMetadataAuthoringService(tagCatalog.Object, groupCatalog.Object, Mock.Of<IRuleTagColorGenerator>());
    }

    private static RuleGroup Group(Guid id, string name) => new(id, name, null, [], []);
}
