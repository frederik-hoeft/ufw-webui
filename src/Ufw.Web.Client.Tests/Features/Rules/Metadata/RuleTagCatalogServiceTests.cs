using Moq;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Api.RuleTags;
using Ufw.Web.Model.V1.RuleTags;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Tests.Features.Rules.Metadata;

[TestClass]
public sealed class RuleTagCatalogServiceTests
{
    [TestMethod]
    public async Task RefreshAsync_NormalizesAndOrdersTagsAsync()
    {
        Mock<IRuleTagApiClient> api = new();
        Guid zetaId = Guid.CreateVersion7();
        Guid alphaId = Guid.CreateVersion7();
        api.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RuleTagInventoryResponse
        {
            Tags =
            [
                new RuleTagItem { Id = zetaId, Name = "  zeta  ", Color = "#aabbcc" },
                new RuleTagItem { Id = alphaId, Name = "Alpha", Color = " #123456 " },
            ],
        });
        RuleTagCatalogService service = new(api.Object);

        IReadOnlyList<RuleTag> tags = await service.RefreshAsync();

        Assert.HasCount(2, tags);
        Assert.AreEqual(alphaId, tags[0].Id);
        Assert.AreEqual("Alpha", tags[0].Name);
        Assert.AreEqual("#123456", tags[0].Color);
        Assert.AreEqual(zetaId, tags[1].Id);
        Assert.AreEqual("zeta", tags[1].Name);
        Assert.AreEqual("#AABBCC", tags[1].Color);
        Assert.AreSame(tags, service.Current);
        Assert.AreEqual(0L, service.Version);
    }

    [TestMethod]
    public async Task RefreshAsync_DuplicateNamesOrInvalidColor_RejectsProtocolResponseAsync()
    {
        Mock<IRuleTagApiClient> duplicateApi = new();
        duplicateApi.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RuleTagInventoryResponse
        {
            Tags =
            [
                Tag("Prod", "#112233"),
                Tag("prod", "#445566"),
            ],
        });
        RuleTagCatalogService duplicateService = new(duplicateApi.Object);
        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => duplicateService.RefreshAsync());

        Mock<IRuleTagApiClient> invalidApi = new();
        invalidApi.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RuleTagInventoryResponse
        {
            Tags = [Tag("prod", "red")],
        });
        RuleTagCatalogService invalidService = new(invalidApi.Object);
        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => invalidService.RefreshAsync());
    }

    [TestMethod]
    public async Task MutationMethods_ReplaceCurrentAndAdvanceVersionAsync()
    {
        Mock<IRuleTagApiClient> api = new();
        Guid tagId = Guid.CreateVersion7();
        api.Setup(client => client.CreateAsync(It.Is<CreateRuleTagRequest>(request => request.Name == "prod" && request.Color == "#112233"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleTagInventoryResponse { Tags = [new RuleTagItem { Id = tagId, Name = "prod", Color = "#112233" }] });
        api.Setup(client => client.UpdateAsync(tagId, It.Is<UpdateRuleTagRequest>(request => request.Name == "production" && request.Color == "#AABBCC"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleTagInventoryResponse { Tags = [new RuleTagItem { Id = tagId, Name = "production", Color = "#AABBCC" }] });
        api.Setup(client => client.DeleteAsync(tagId, It.IsAny<CancellationToken>())).ReturnsAsync(new RuleTagInventoryResponse());
        RuleTagCatalogService service = new(api.Object);

        _ = await service.CreateAsync("prod", "#112233");
        Assert.AreEqual(1L, service.Version);
        Assert.AreEqual("prod", service.Current.Single().Name);

        _ = await service.UpdateAsync(tagId, "production", "#AABBCC");
        Assert.AreEqual(2L, service.Version);
        Assert.AreEqual("production", service.Current.Single().Name);

        _ = await service.DeleteAsync(tagId);
        Assert.AreEqual(3L, service.Version);
        Assert.IsEmpty(service.Current);
    }

    private static RuleTagItem Tag(string name, string color) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        Color = color,
    };
}
