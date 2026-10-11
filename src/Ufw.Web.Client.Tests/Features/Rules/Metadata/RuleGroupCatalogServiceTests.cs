using Moq;
using Ufw.Shared.Management.Rules;
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
                new RuleGroupItem(zetaId, "  zeta  ", "  later  ", ["rule-b", "rule-a"]) { TemplateIds = [Guid.Parse("0199aabb-ccdd-7eef-8000-000000000099")] },
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
        CollectionAssert.AreEqual(new[] { Guid.Parse("0199aabb-ccdd-7eef-8000-000000000099") }, groups[1].TemplateIds.ToArray());
        Assert.AreSame(groups, service.Current);
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
    public async Task RefreshAsync_InvalidTemplateReferencesRejectProtocolResponseAsync()
    {
        Mock<IRuleGroupApiClient> api = new();
        api.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RuleGroupInventoryResponse
        {
            Groups = [new RuleGroupItem(Guid.CreateVersion7(), "ops", null, []) { TemplateIds = [Guid.Empty] }],
        });
        RuleGroupCatalogService service = new(api.Object);

        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => service.RefreshAsync());
    }

    [TestMethod]
    public async Task InvalidResponse_DoesNotReplacePreviouslyLoadedCatalogAsync()
    {
        Mock<IRuleGroupApiClient> api = new();
        api.SetupSequence(client => client.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleGroupInventoryResponse { Groups = [Group("valid")] })
            .ReturnsAsync(new RuleGroupInventoryResponse { Groups = [Group("duplicate"), Group("duplicate")] });
        api.Setup(client => client.CreateAsync(It.IsAny<CreateRuleGroupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleGroupInventoryResponse { Groups = [Group("duplicate"), Group("duplicate")] });
        RuleGroupCatalogService service = new(api.Object);
        IReadOnlyList<RuleGroup> current = await service.RefreshAsync();

        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => service.RefreshAsync());
        Assert.AreSame(current, service.Current);

        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => service.CreateAsync("duplicate"));
        Assert.AreSame(current, service.Current);
    }

    [TestMethod]
    public async Task MutationMethods_ReplaceCurrentFromServerResponsesAsync()
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
        Assert.AreEqual("ops", service.Current.Single().Name);

        _ = await service.UpdateAsync(groupId, "operations", "managed");
        Assert.AreEqual("operations", service.Current.Single().Name);
        Assert.AreEqual("managed", service.Current.Single().Comment);

        _ = await service.DeleteAsync(groupId);
        Assert.IsEmpty(service.Current);
    }

    private static RuleGroupItem Group(string name) => new(Guid.CreateVersion7(), name, null, []);
}
