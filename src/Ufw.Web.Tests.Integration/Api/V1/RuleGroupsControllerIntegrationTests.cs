using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Data;
using Ufw.Web.Data.Access;
using Ufw.Web.Data.Access.Rules;
using Ufw.Web.Data.Access.Rules.Metadata;
using Ufw.Web.Data.Model;
using Ufw.Web.Model.V1.RuleGroups;
using Ufw.Web.Services.Rules;
using Ufw.Web.Tests.Integration.Support;

namespace Ufw.Web.Tests.Integration.Api.V1;

[TestClass]
internal sealed class RuleGroupsControllerIntegrationTests : ControllerIntegrationTest<RuleGroupsController>
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public Task CrudAsync_PersistsEmptyGroupWithUuidV7PublicIdentityAsync() =>
        UsingComponentAsync(async (controller, serviceProvider, cancellationToken) =>
        {
            IActionResult createResult = await controller.CreateAsync(
                new CreateRuleGroupRequest { Name = "  Platform  ", Comment = "  managed rules  " },
                cancellationToken);
            OkObjectResult createOk = Assert.IsInstanceOfType<OkObjectResult>(createResult);
            RuleGroupInventoryResponse created = Assert.IsInstanceOfType<RuleGroupInventoryResponse>(createOk.Value);
            RuleGroupItem group = created.Groups.Single();
            Assert.AreEqual('7', group.Id.ToString("D")[14]);
            Assert.AreEqual("Platform", group.Name);
            Assert.AreEqual("managed rules", group.Comment);
            Assert.IsEmpty(group.RuleIds);

            IActionResult updateResult = await controller.UpdateAsync(
                group.Id,
                new UpdateRuleGroupRequest { Name = "Core", Comment = "  " },
                cancellationToken);
            OkObjectResult updateOk = Assert.IsInstanceOfType<OkObjectResult>(updateResult);
            RuleGroupInventoryResponse updated = Assert.IsInstanceOfType<RuleGroupInventoryResponse>(updateOk.Value);
            RuleGroupItem updatedGroup = updated.Groups.Single();
            Assert.AreEqual(group.Id, updatedGroup.Id);
            Assert.AreEqual("Core", updatedGroup.Name);
            Assert.IsNull(updatedGroup.Comment);

            ApplicationDbContext context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            context.ChangeTracker.Clear();
            RuleGroupEntry persisted = await context.Set<RuleGroupEntry>().SingleAsync(cancellationToken);
            Assert.IsTrue(persisted.Id > 0);
            Assert.AreEqual(group.Id, persisted.PublicId);

            IActionResult deleteResult = await controller.DeleteAsync(group.Id, cancellationToken);
            OkObjectResult deleteOk = Assert.IsInstanceOfType<OkObjectResult>(deleteResult);
            RuleGroupInventoryResponse deleted = Assert.IsInstanceOfType<RuleGroupInventoryResponse>(deleteOk.Value);
            Assert.IsEmpty(deleted.Groups);
            context.ChangeTracker.Clear();
            Assert.IsFalse(await context.Set<RuleGroupEntry>().AnyAsync(cancellationToken));
        }, TestContext.CancellationToken);

    [TestMethod]
    public Task DeleteAsync_InUseGroupReturnsConflictAndInventoryIncludesSemanticMemberAsync() =>
        UsingComponentAsync(async (controller, serviceProvider, cancellationToken) =>
        {
            IActionResult createResult = await controller.CreateAsync(new CreateRuleGroupRequest { Name = "Batch" }, cancellationToken);
            RuleGroupItem group = Assert.IsInstanceOfType<RuleGroupInventoryResponse>(Assert.IsInstanceOfType<OkObjectResult>(createResult).Value).Groups.Single();

            IRuleMetadataDataAccess metadata = serviceProvider.GetRequiredService<IRuleMetadataDataAccess>();
            DataMutationResult<RuleMetadataItem?> saved = await metadata.SaveAsync("sha256:member", new RuleMetadataValues(Notes: null, TagIds: [], GroupId: group.Id), cancellationToken);
            Assert.IsTrue(saved.IsSuccess);

            ActionResult<RuleGroupInventoryResponse> inventoryResult = await controller.GetAsync(cancellationToken);
            RuleGroupInventoryResponse inventory = Assert.IsInstanceOfType<RuleGroupInventoryResponse>(Assert.IsInstanceOfType<OkObjectResult>(inventoryResult.Result).Value);
            RuleGroupItem inventoryGroup = inventory.Groups.Single();
            Assert.HasCount(1, inventoryGroup.RuleIds);
            Assert.AreEqual("sha256:member", inventoryGroup.RuleIds[0]);

            IActionResult deleteResult = await controller.DeleteAsync(group.Id, cancellationToken);
            ConflictObjectResult conflict = Assert.IsInstanceOfType<ConflictObjectResult>(deleteResult);
            Assert.AreEqual(StatusCodes.Status409Conflict, conflict.StatusCode);
        }, TestContext.CancellationToken);

    [TestMethod]
    public Task ListAndCreateAsync_UseBoundedSqlCommandCountsAsync() =>
        UsingComponentAsync(async (controller, serviceProvider, cancellationToken) =>
        {
            ApplicationDbContext context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            for (int i = 0; i < 8; ++i)
            {
                context.Add(new RuleGroupEntry { Name = $"Group {i:D2}" });
            }
            await context.SaveChangesAsync(cancellationToken);
            context.ChangeTracker.Clear();

            SqlCommandCounterInterceptor commands = serviceProvider.GetRequiredService<SqlCommandCounterInterceptor>();
            commands.Reset();

            ActionResult<RuleGroupInventoryResponse> listResult = await controller.GetAsync(cancellationToken);

            _ = Assert.IsInstanceOfType<OkObjectResult>(listResult.Result);
            Assert.IsLessThanOrEqualTo(3, commands.CommandCount, $"Rule-group list unexpectedly executed {commands.CommandCount} SQL commands.");

            commands.Reset();
            IActionResult createResult = await controller.CreateAsync(new CreateRuleGroupRequest { Name = "Created" }, cancellationToken);

            _ = Assert.IsInstanceOfType<OkObjectResult>(createResult);
            Assert.IsLessThanOrEqualTo(5, commands.CommandCount, $"Rule-group create plus refreshed inventory unexpectedly executed {commands.CommandCount} SQL commands.");
        }, TestContext.CancellationToken);
}
