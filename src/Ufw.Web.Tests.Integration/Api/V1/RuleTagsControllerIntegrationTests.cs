using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Model.V1.RuleTags;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;

namespace Ufw.Web.Tests.Integration.Api.V1;

[TestClass]
public sealed class RuleTagsControllerIntegrationTests : ControllerIntegrationTest<RuleTagsController>
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public Task CrudAsync_PersistsReusableTagWithUuidV7PublicIdentityAsync() =>
        UsingComponentAsync(async (controller, serviceProvider, cancellationToken) =>
        {
            IActionResult createResult = await controller.CreateAsync(
                new CreateRuleTagRequest { Name = "  Production  ", Color = "#12ab34" },
                cancellationToken);
            OkObjectResult createOk = Assert.IsInstanceOfType<OkObjectResult>(createResult);
            RuleTagInventoryResponse created = Assert.IsInstanceOfType<RuleTagInventoryResponse>(createOk.Value);
            RuleTagItem tag = created.Tags.Single();
            Assert.AreEqual('7', tag.Id.ToString("D")[14]);
            Assert.AreEqual("Production", tag.Name);
            Assert.AreEqual("#12AB34", tag.Color);

            IActionResult updateResult = await controller.UpdateAsync(
                tag.Id,
                new UpdateRuleTagRequest { Name = "prod", Color = "#0011aa" },
                cancellationToken);
            OkObjectResult updateOk = Assert.IsInstanceOfType<OkObjectResult>(updateResult);
            RuleTagInventoryResponse updated = Assert.IsInstanceOfType<RuleTagInventoryResponse>(updateOk.Value);
            RuleTagItem updatedTag = updated.Tags.Single();
            Assert.AreEqual(tag.Id, updatedTag.Id);
            Assert.AreEqual("prod", updatedTag.Name);
            Assert.AreEqual("#0011AA", updatedTag.Color);

            ApplicationDbContext context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            context.ChangeTracker.Clear();
            RuleTagEntry persisted = await context.Set<RuleTagEntry>().SingleAsync(cancellationToken);
            Assert.IsTrue(persisted.Id > 0);
            Assert.AreEqual(tag.Id, persisted.PublicId);

            IActionResult deleteResult = await controller.DeleteAsync(tag.Id, cancellationToken);
            OkObjectResult deleteOk = Assert.IsInstanceOfType<OkObjectResult>(deleteResult);
            RuleTagInventoryResponse deleted = Assert.IsInstanceOfType<RuleTagInventoryResponse>(deleteOk.Value);
            Assert.IsEmpty(deleted.Tags);
            context.ChangeTracker.Clear();
            Assert.IsFalse(await context.Set<RuleTagEntry>().AnyAsync(cancellationToken));
        }, TestContext.CancellationToken);
}
