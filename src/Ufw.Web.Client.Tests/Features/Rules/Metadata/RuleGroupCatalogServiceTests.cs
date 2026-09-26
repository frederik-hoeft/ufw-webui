using Moq;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Api.RuleGroups;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Model.V1.RuleGroups;

namespace Ufw.Web.Client.Tests.Features.Rules.Metadata;

[TestClass]
public sealed class RuleGroupCatalogServiceTests
{
    [TestMethod]
    public async Task RefreshAsync_NormalizesOrdersAndPreservesMemberIdentitiesAsync()
    {
        Mock<IRuleGroupApiClient> api = new();
        Guid zetaId = Guid.CreateVersion7();
        Guid alphaId = Guid.CreateVersion7();
        api.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RuleGroupInventoryResponse
        {
            Groups =
            [
                new RuleGroupItem(zetaId, "  zeta  ", "  later  ", ["rule-b", "rule-a"]),
                new RuleGroupItem(alphaId, "Alpha", "   ", []),
            ],
        });
        RuleGroupCatalogService service = new(api.Object);

        IReadOnlyList<RuleGroup> groups = await service.RefreshAsync();

        Assert.HasCount(2, groups);
        Assert.AreEqual(alphaId, groups[0].Id);
        Assert.AreEqual("Alpha", groups[0].Name);
        Assert.IsNull(groups[0].Comment);
        Assert.AreEqual(zetaId, groups[1].Id);
        Assert.AreEqual("zeta", groups[1].Name);
        Assert.AreEqual("later", groups[1].Comment);
        CollectionAssert.AreEqual(new[] { "rule-b", "rule-a" }, groups[1].RuleIds.ToArray());
        Assert.AreSame(groups, service.Current);
        Assert.AreEqual(0L, service.Version);
    }

    [TestMethod]
    public async Task RefreshAsync_DuplicateNamesOrMemberIdentities_RejectsProtocolResponseAsync()
    {
        Mock<IRuleGroupApiClient> duplicateNamesApi = new();
        duplicateNamesApi.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RuleGroupInventoryResponse
        {
            Groups = [Group("Ops"), Group("ops")],
        });
        RuleGroupCatalogService duplicateNames = new(duplicateNamesApi.Object);
        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => duplicateNames.RefreshAsync());

        Mock<IRuleGroupApiClient> duplicateMembersApi = new();
        duplicateMembersApi.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RuleGroupInventoryResponse
        {
            Groups = [new RuleGroupItem(Guid.CreateVersion7(), "ops", null, ["rule", "rule"])],
        });
        RuleGroupCatalogService duplicateMembers = new(duplicateMembersApi.Object);
        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => duplicateMembers.RefreshAsync());
    }

    [TestMethod]
    public async Task MutationMethods_ReplaceCurrentAndAdvanceVersionAsync()
    {
        Mock<IRuleGroupApiClient> api = new();
        Guid groupId = Guid.CreateVersion7();
        api.Setup(client => client.CreateAsync(It.Is<CreateRuleGroupRequest>(request => request.Name == "ops" && request.Comment == null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleGroupInventoryResponse { Groups = [new RuleGroupItem(groupId, "ops", null, [])] });
        api.Setup(client => client.UpdateAsync(groupId, It.Is<UpdateRuleGroupRequest>(request => request.Name == "operations" && request.Comment == "managed"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleGroupInventoryResponse { Groups = [new RuleGroupItem(groupId, "operations", "managed", [])] });
        api.Setup(client => client.DeleteAsync(groupId, It.IsAny<CancellationToken>())).ReturnsAsync(new RuleGroupInventoryResponse());
        RuleGroupCatalogService service = new(api.Object);

        _ = await service.CreateAsync("ops");
        Assert.AreEqual(1L, service.Version);
        Assert.AreEqual("ops", service.Current.Single().Name);

        _ = await service.UpdateAsync(groupId, "operations", "managed");
        Assert.AreEqual(2L, service.Version);
        Assert.AreEqual("operations", service.Current.Single().Name);
        Assert.AreEqual("managed", service.Current.Single().Comment);

        _ = await service.DeleteAsync(groupId);
        Assert.AreEqual(3L, service.Version);
        Assert.IsEmpty(service.Current);
    }

    private static RuleGroupItem Group(string name) => new(Guid.CreateVersion7(), name, null, []);
}
