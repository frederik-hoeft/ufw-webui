using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ufw.Shared.Firewall;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Ufw.Web.Model.V1.RuleGroups;
using Ufw.Web.Model.V1.RuleTags;
using Ufw.Web.Model.V1.RuleTemplates;

namespace Ufw.Web.Tests.Integration.Api.V1;

[TestClass]
public sealed class RuleTemplatesControllerIntegrationTests : ControllerIntegrationTest<RuleTemplatesController>
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public Task CrudAsync_PersistsNormalizedRuleAndMetadataWithStableUuidV7IdentityAsync() =>
        UsingComponentAsync(async (controller, serviceProvider, cancellationToken) =>
        {
            RuleTagsController tags = serviceProvider.GetRequiredService<RuleTagsController>();
            RuleGroupsController groups = serviceProvider.GetRequiredService<RuleGroupsController>();
            Guid tagId = (await CreateTagAsync(tags, "Ingress", cancellationToken)).Id;
            Guid groupId = (await CreateGroupAsync(groups, "Platform", cancellationToken)).Id;

            IActionResult createResult = await controller.CreateAsync(new CreateRuleTemplateRequest
            {
                Name = "  Web ingress  ",
                Description = "  reverse proxy  ",
                Rule = new FirewallRuleSpecification
                {
                    Action = FirewallAction.Allow,
                    AddressFamily = FirewallAddressFamily.Any,
                    Direction = FirewallDirection.In,
                    Protocol = FirewallProtocol.Tcp,
                    Source = " 10.20.30.25/24 ",
                    Destination = " any ",
                    DestinationPorts = "8443,443",
                    DestinationInterface = " eno1 ",
                    Comment = "  published web  ",
                },
                Notes = "  restore after maintenance  ",
                TagIds = [tagId, tagId],
                GroupId = groupId,
            }, cancellationToken);

            RuleTemplateInventoryResponse created = Assert.IsInstanceOfType<RuleTemplateInventoryResponse>(Assert.IsInstanceOfType<OkObjectResult>(createResult).Value);
            RuleTemplateItem template = created.Templates.Single();
            Assert.AreEqual('7', template.Id.ToString("D")[14]);
            Assert.AreEqual("Web ingress", template.Name);
            Assert.AreEqual("reverse proxy", template.Description);
            Assert.AreEqual(FirewallAddressFamily.IPv4, template.Rule.AddressFamily);
            Assert.AreEqual("10.20.30.0/24", template.Rule.Source);
            Assert.AreEqual("443,8443", template.Rule.DestinationPorts);
            Assert.AreEqual("eno1", template.Rule.DestinationInterface);
            Assert.AreEqual("published web", template.Rule.Comment);
            Assert.AreEqual("restore after maintenance", template.Notes);
            Assert.AreEqual(tagId, template.Tags.Single().Id);
            Assert.AreEqual(groupId, template.Group?.Id);

            ApplicationDbContext context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            context.ChangeTracker.Clear();
            RuleTemplateEntry persisted = await context.Set<RuleTemplateEntry>().Include(static entry => entry.Tags).SingleAsync(cancellationToken);
            Assert.IsTrue(persisted.Id > 0);
            Assert.AreEqual(template.Id, persisted.PublicId);
            Assert.AreEqual(1, persisted.Tags.Count);
            Assert.AreEqual(groupId, (await context.Set<RuleGroupEntry>().SingleAsync(cancellationToken)).PublicId);

            IActionResult updateResult = await controller.UpdateAsync(template.Id, new UpdateRuleTemplateRequest
            {
                Name = "Web service",
                Description = "  ",
                Rule = new FirewallRuleSpecification
                {
                    Action = FirewallAction.Deny,
                    AddressFamily = FirewallAddressFamily.Any,
                    Direction = FirewallDirection.Out,
                    Protocol = FirewallProtocol.Udp,
                    Source = "any",
                    Destination = "2001:db8::10",
                    DestinationPorts = "53",
                    SourceInterface = "future0",
                },
            }, cancellationToken);

            RuleTemplateInventoryResponse updated = Assert.IsInstanceOfType<RuleTemplateInventoryResponse>(Assert.IsInstanceOfType<OkObjectResult>(updateResult).Value);
            RuleTemplateItem updatedTemplate = updated.Templates.Single();
            Assert.AreEqual(template.Id, updatedTemplate.Id);
            Assert.AreEqual("Web service", updatedTemplate.Name);
            Assert.IsNull(updatedTemplate.Description);
            Assert.AreEqual(FirewallAddressFamily.IPv6, updatedTemplate.Rule.AddressFamily);
            Assert.AreEqual("future0", updatedTemplate.Rule.SourceInterface);
            Assert.IsNull(updatedTemplate.Notes);
            Assert.IsEmpty(updatedTemplate.Tags);
            Assert.IsNull(updatedTemplate.Group);

            IActionResult deleteResult = await controller.DeleteAsync(template.Id, cancellationToken);
            RuleTemplateInventoryResponse deleted = Assert.IsInstanceOfType<RuleTemplateInventoryResponse>(Assert.IsInstanceOfType<OkObjectResult>(deleteResult).Value);
            Assert.IsEmpty(deleted.Templates);
            context.ChangeTracker.Clear();
            Assert.IsFalse(await context.Set<RuleTemplateEntry>().AnyAsync(cancellationToken));
            Assert.IsFalse(await context.Set<RuleTemplateTagEntry>().AnyAsync(cancellationToken));
        }, TestContext.CancellationToken);

    [TestMethod]
    public Task NameConflict_IsCaseInsensitiveAndDoesNotOverwriteAsync() =>
        UsingComponentAsync(async (controller, serviceProvider, cancellationToken) =>
        {
            _ = serviceProvider;
            IActionResult firstResult = await controller.CreateAsync(Request("Maintenance SSH"), cancellationToken);
            RuleTemplateItem first = Assert.IsInstanceOfType<RuleTemplateInventoryResponse>(Assert.IsInstanceOfType<OkObjectResult>(firstResult).Value).Templates.Single();

            IActionResult conflictResult = await controller.CreateAsync(Request("maintenance ssh"), cancellationToken);

            ConflictObjectResult conflict = Assert.IsInstanceOfType<ConflictObjectResult>(conflictResult);
            Assert.AreEqual(StatusCodes.Status409Conflict, conflict.StatusCode);
            RuleTemplateInventoryResponse inventory = Assert.IsInstanceOfType<RuleTemplateInventoryResponse>(Assert.IsInstanceOfType<OkObjectResult>((await controller.GetAsync(cancellationToken)).Result).Value);
            RuleTemplateItem persisted = inventory.Templates.Single();
            Assert.AreEqual(first.Id, persisted.Id);
            Assert.AreEqual("Maintenance SSH", persisted.Name);
        }, TestContext.CancellationToken);

    [TestMethod]
    public Task MissingMetadataDependency_UpdateFailsWithoutChangingExistingTemplateAsync() =>
        UsingComponentAsync(async (controller, serviceProvider, cancellationToken) =>
        {
            _ = serviceProvider;
            RuleTemplateItem created = Assert.IsInstanceOfType<RuleTemplateInventoryResponse>(Assert.IsInstanceOfType<OkObjectResult>(
                await controller.CreateAsync(Request("Stable"), cancellationToken)).Value).Templates.Single();

            UpdateRuleTemplateRequest update = new()
            {
                Name = "Changed",
                Rule = ValidRule(),
                TagIds = [Guid.CreateVersion7()],
            };
            IActionResult updateResult = await controller.UpdateAsync(created.Id, update, cancellationToken);

            BadRequestObjectResult badRequest = Assert.IsInstanceOfType<BadRequestObjectResult>(updateResult);
            Assert.AreEqual(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            RuleTemplateInventoryResponse inventory = Assert.IsInstanceOfType<RuleTemplateInventoryResponse>(Assert.IsInstanceOfType<OkObjectResult>((await controller.GetAsync(cancellationToken)).Result).Value);
            RuleTemplateItem persisted = inventory.Templates.Single();
            Assert.AreEqual(created.Id, persisted.Id);
            Assert.AreEqual("Stable", persisted.Name);
            Assert.IsEmpty(persisted.Tags);
        }, TestContext.CancellationToken);



    [TestMethod]
    public Task MissingGroupDependency_CreateFailsWithoutPersistingTemplateAsync() =>
        UsingComponentAsync(async (controller, serviceProvider, cancellationToken) =>
        {
            CreateRuleTemplateRequest request = new()
            {
                Name = "Missing group",
                Rule = ValidRule(),
                GroupId = Guid.CreateVersion7(),
            };

            IActionResult createResult = await controller.CreateAsync(request, cancellationToken);

            BadRequestObjectResult badRequest = Assert.IsInstanceOfType<BadRequestObjectResult>(createResult);
            Assert.AreEqual(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            ApplicationDbContext context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            context.ChangeTracker.Clear();
            Assert.IsFalse(await context.Set<RuleTemplateEntry>().AnyAsync(cancellationToken));
        }, TestContext.CancellationToken);

    [TestMethod]
    public Task UpdateAsync_DatabaseFailureRollsBackExistingTemplateAsync() =>
        UsingComponentAsync(async (controller, serviceProvider, cancellationToken) =>
        {
            RuleTemplateItem created = Assert.IsInstanceOfType<RuleTemplateInventoryResponse>(Assert.IsInstanceOfType<OkObjectResult>(
                await controller.CreateAsync(Request("Stable"), cancellationToken)).Value).Templates.Single();
            ApplicationDbContext context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER RejectRuleTemplateUpdate BEFORE UPDATE ON RuleTemplates BEGIN SELECT RAISE(ABORT, 'forced template update failure'); END;",
                cancellationToken);

            await Assert.ThrowsExactlyAsync<DbUpdateException>(() => controller.UpdateAsync(created.Id, new UpdateRuleTemplateRequest
            {
                Name = "Changed",
                Description = "should roll back",
                Rule = ValidRule(),
            }, cancellationToken));

            context.ChangeTracker.Clear();
            RuleTemplateEntry persisted = await context.Set<RuleTemplateEntry>().SingleAsync(cancellationToken);
            Assert.AreEqual(created.Id, persisted.PublicId);
            Assert.AreEqual("Stable", persisted.Name);
            Assert.IsNull(persisted.Description);
        }, TestContext.CancellationToken);

    [TestMethod]
    public Task TemplateReferences_BlockTagAndGroupDeletionUntilTemplateIsDeletedAsync() =>
        UsingComponentAsync(async (controller, serviceProvider, cancellationToken) =>
        {
            RuleTagsController tags = serviceProvider.GetRequiredService<RuleTagsController>();
            RuleGroupsController groups = serviceProvider.GetRequiredService<RuleGroupsController>();
            RuleTagItem tag = await CreateTagAsync(tags, "Protected", cancellationToken);
            RuleGroupItem group = await CreateGroupAsync(groups, "Protected group", cancellationToken);

            CreateRuleTemplateRequest request = Request("Dependency owner");
            request = new CreateRuleTemplateRequest
            {
                Name = request.Name,
                Rule = request.Rule,
                TagIds = [tag.Id],
                GroupId = group.Id,
            };
            RuleTemplateItem template = Assert.IsInstanceOfType<RuleTemplateInventoryResponse>(Assert.IsInstanceOfType<OkObjectResult>(
                await controller.CreateAsync(request, cancellationToken)).Value).Templates.Single();

            IActionResult tagDelete = await tags.DeleteAsync(tag.Id, cancellationToken);
            IActionResult groupDelete = await groups.DeleteAsync(group.Id, cancellationToken);
            Assert.IsInstanceOfType<ConflictObjectResult>(tagDelete);
            Assert.IsInstanceOfType<ConflictObjectResult>(groupDelete);

            RuleGroupInventoryResponse groupInventory = Assert.IsInstanceOfType<RuleGroupInventoryResponse>(Assert.IsInstanceOfType<OkObjectResult>(
                (await groups.GetAsync(cancellationToken)).Result).Value);
            RuleGroupItem protectedGroup = groupInventory.Groups.Single();
            Assert.IsEmpty(protectedGroup.RuleIds);
            CollectionAssert.AreEqual(new[] { template.Id }, protectedGroup.TemplateIds.ToArray());

            _ = await controller.DeleteAsync(template.Id, cancellationToken);
            Assert.IsInstanceOfType<OkObjectResult>(await tags.DeleteAsync(tag.Id, cancellationToken));
            Assert.IsInstanceOfType<OkObjectResult>(await groups.DeleteAsync(group.Id, cancellationToken));
        }, TestContext.CancellationToken);

    private static CreateRuleTemplateRequest Request(string name) => new()
    {
        Name = name,
        Rule = ValidRule(),
    };

    private static FirewallRuleSpecification ValidRule() => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.Any,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        Source = "any",
        Destination = "any",
        DestinationPorts = "22",
    };

    private static async Task<RuleTagItem> CreateTagAsync(RuleTagsController controller, string name, CancellationToken cancellationToken)
    {
        IActionResult result = await controller.CreateAsync(new CreateRuleTagRequest { Name = name, Color = "#123456" }, cancellationToken);
        return Assert.IsInstanceOfType<RuleTagInventoryResponse>(Assert.IsInstanceOfType<OkObjectResult>(result).Value).Tags.Single();
    }

    private static async Task<RuleGroupItem> CreateGroupAsync(RuleGroupsController controller, string name, CancellationToken cancellationToken)
    {
        IActionResult result = await controller.CreateAsync(new CreateRuleGroupRequest { Name = name }, cancellationToken);
        return Assert.IsInstanceOfType<RuleGroupInventoryResponse>(Assert.IsInstanceOfType<OkObjectResult>(result).Value).Groups.Single();
    }
}
