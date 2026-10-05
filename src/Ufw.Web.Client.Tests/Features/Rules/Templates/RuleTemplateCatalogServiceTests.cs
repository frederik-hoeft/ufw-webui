using Moq;
using Ufw.Shared.Firewall;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Api.RuleTemplates;
using Ufw.Web.Client.Features.Rules.Templates;
using Ufw.Web.Model.V1.RuleTemplates;

namespace Ufw.Web.Client.Tests.Features.Rules.Templates;

[TestClass]
public sealed class RuleTemplateCatalogServiceTests
{
    [TestMethod]
    public async Task RefreshAsync_NormalizesAndOrdersTemplatesAsync()
    {
        Mock<IRuleTemplateApiClient> api = new();
        Guid zetaId = Guid.CreateVersion7();
        Guid alphaId = Guid.CreateVersion7();
        Guid tagId = Guid.CreateVersion7();
        Guid groupId = Guid.CreateVersion7();
        api.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RuleTemplateInventoryResponse
        {
            Templates =
            [
                Template(zetaId, "  zeta  ", FirewallAddressFamily.IPv6),
                new RuleTemplateItem
                {
                    Id = alphaId,
                    Name = "Alpha",
                    Description = "  edge ingress  ",
                    Rule = Rule(FirewallAddressFamily.Any),
                    Notes = "  note  ",
                    Tags = [new RuleTagItem(tagId, "  prod  ", " #aabbcc ")],
                    Group = new RuleGroupSummary(groupId, "  perimeter  ", "  shared  "),
                },
            ],
        });
        RuleTemplateCatalogService service = new(api.Object);

        IReadOnlyList<RuleTemplate> templates = await service.RefreshAsync();

        Assert.HasCount(2, templates);
        RuleTemplate alpha = templates[0];
        Assert.AreEqual(alphaId, alpha.Id);
        Assert.AreEqual("Alpha", alpha.Name);
        Assert.AreEqual("edge ingress", alpha.Description);
        Assert.AreEqual("note", alpha.Notes);
        Assert.AreEqual(tagId, alpha.Tags.Single().Id);
        Assert.AreEqual("prod", alpha.Tags.Single().Name);
        Assert.AreEqual("#AABBCC", alpha.Tags.Single().Color);
        Assert.AreEqual(groupId, alpha.GroupId);
        Assert.AreEqual("perimeter", alpha.Group!.Name);
        Assert.AreEqual("shared", alpha.Group.Comment);
        Assert.AreEqual(FirewallAddressFamily.IPv6, templates[1].Rule.AddressFamily);
        Assert.AreSame(templates, service.Current);
        Assert.AreEqual(0L, service.Version);
    }

    [TestMethod]
    public async Task RefreshAsync_DuplicateNamesAreAllowedButDuplicateIdentitiesAreRejectedAsync()
    {
        Mock<IRuleTemplateApiClient> duplicateApi = new();
        duplicateApi.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RuleTemplateInventoryResponse
        {
            Templates =
            [
                Template(Guid.CreateVersion7(), "Prod", FirewallAddressFamily.Any),
                Template(Guid.CreateVersion7(), "prod", FirewallAddressFamily.Any),
            ],
        });
        IReadOnlyList<RuleTemplate> sameName = await new RuleTemplateCatalogService(duplicateApi.Object).RefreshAsync();
        Assert.HasCount(2, sameName);
        Assert.AreNotEqual(sameName[0].Id, sameName[1].Id);

        Guid duplicateId = Guid.CreateVersion7();
        duplicateApi.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RuleTemplateInventoryResponse
        {
            Templates = [Template(duplicateId, "Prod", FirewallAddressFamily.Any), Template(duplicateId, "Other", FirewallAddressFamily.Any)],
        });
        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => new RuleTemplateCatalogService(duplicateApi.Object).RefreshAsync());

        Mock<IRuleTemplateApiClient> invalidApi = new();
        invalidApi.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new RuleTemplateInventoryResponse
        {
            Templates = [Template(Guid.CreateVersion7(), "broken", FirewallAddressFamily.IPv4, source: "2001:db8::1")],
        });
        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => new RuleTemplateCatalogService(invalidApi.Object).RefreshAsync());
    }

    [TestMethod]
    public async Task MutationFailure_PreservesCurrentInventoryAndVersionAsync()
    {
        Mock<IRuleTemplateApiClient> api = new();
        Guid id = Guid.CreateVersion7();
        RuleTemplateInventoryResponse baseline = new([Template(id, "web", FirewallAddressFamily.Any)]);
        api.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(baseline);
        ApiRequestException failure = new(System.Net.HttpStatusCode.Conflict, "conflict");
        api.Setup(client => client.CreateAsync(It.IsAny<CreateRuleTemplateRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        api.Setup(client => client.UpdateAsync(id, It.IsAny<UpdateRuleTemplateRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        api.Setup(client => client.DeleteAsync(id, It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        RuleTemplateCatalogService service = new(api.Object);
        IReadOnlyList<RuleTemplate> current = await service.RefreshAsync();
        RuleTemplateDefinition definition = new("web", null, Rule(FirewallAddressFamily.Any), null, [], null);

        await Assert.ThrowsExactlyAsync<ApiRequestException>(() => service.CreateAsync(definition));
        Assert.AreSame(current, service.Current);
        Assert.AreEqual(0L, service.Version);

        await Assert.ThrowsExactlyAsync<ApiRequestException>(() => service.UpdateAsync(id, definition));
        Assert.AreSame(current, service.Current);
        Assert.AreEqual(0L, service.Version);

        await Assert.ThrowsExactlyAsync<ApiRequestException>(() => service.DeleteAsync(id));
        Assert.AreSame(current, service.Current);
        Assert.AreEqual(0L, service.Version);
    }

    [TestMethod]
    public async Task MutationMethods_UseDefinitionAndAdvanceVersionAsync()
    {
        Mock<IRuleTemplateApiClient> api = new();
        Guid id = Guid.CreateVersion7();
        Guid tagId = Guid.CreateVersion7();
        Guid groupId = Guid.CreateVersion7();
        RuleTemplateDefinition definition = new("web", "http", Rule(FirewallAddressFamily.IPv6), "notes", [tagId], groupId);
        api.Setup(client => client.CreateAsync(It.Is<CreateRuleTemplateRequest>(request => Matches(request, definition)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleTemplateInventoryResponse([Template(id, "web", FirewallAddressFamily.IPv6)]));
        api.Setup(client => client.UpdateAsync(id, It.Is<UpdateRuleTemplateRequest>(request => Matches(request, definition)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleTemplateInventoryResponse([Template(id, "web", FirewallAddressFamily.IPv6)]));
        api.Setup(client => client.DeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new RuleTemplateInventoryResponse());
        RuleTemplateCatalogService service = new(api.Object);

        _ = await service.CreateAsync(definition);
        Assert.AreEqual(1L, service.Version);
        _ = await service.UpdateAsync(id, definition);
        Assert.AreEqual(2L, service.Version);
        _ = await service.DeleteAsync(id);
        Assert.AreEqual(3L, service.Version);
        Assert.IsEmpty(service.Current);
    }

    private static bool Matches(RuleTemplateRequest request, RuleTemplateDefinition definition) => request.Name == definition.Name
        && request.Description == definition.Description
        && request.Rule.AddressFamily == definition.Rule.AddressFamily
        && request.Notes == definition.Notes
        && request.TagIds.SequenceEqual(definition.TagIds)
        && request.GroupId == definition.GroupId;

    private static RuleTemplateItem Template(Guid id, string name, FirewallAddressFamily family, string? source = null) => new()
    {
        Id = id,
        Name = name,
        Rule = Rule(family, source),
    };

    private static FirewallRuleSpecification Rule(FirewallAddressFamily family, string? source = null) => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = family,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        Source = source ?? RuleSpecificationNormalizer.ANY,
        Destination = RuleSpecificationNormalizer.ANY,
        DestinationPorts = "443",
    };
}
