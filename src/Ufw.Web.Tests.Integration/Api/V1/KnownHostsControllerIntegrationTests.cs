using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ufw.Shared.Firewall;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Api.V1.Models.KnownHosts;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Ufw.Web.Tests.Integration.Support;

namespace Ufw.Web.Tests.Integration.Api.V1;

[TestClass]
public sealed class KnownHostsControllerIntegrationTests : ControllerIntegrationTest<KnownHostsController>
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public Task CrudAsync_PersistsCanonicalAspOwnedAliasesAcrossControllerServiceAndRepositoryLayersAsync() =>
        UsingComponentAsync(async (controller, serviceProvider, cancellationToken) =>
        {
            IActionResult createResult = await controller.CreateAsync(
                new CreateKnownHostRequest
                {
                    Name = "  database  ",
                    Address = "192.0.2.129/24",
                    Comment = "  production  ",
                    IsVisible = true,
                },
                cancellationToken);
            OkObjectResult createOk = Assert.IsInstanceOfType<OkObjectResult>(createResult);
            KnownHostInventoryResponse created = Assert.IsInstanceOfType<KnownHostInventoryResponse>(createOk.Value);
            KnownHostItem item = created.Hosts.Single();
            Assert.AreEqual("database", item.Name);
            Assert.AreEqual("192.0.2.0/24", item.Address);
            Assert.AreEqual(FirewallAddressFamily.IPv4, item.AddressFamily);
            Assert.AreEqual("production", item.Comment);
            Assert.AreEqual('7', item.Id.ToString("D")[14]);

            IActionResult updateResult = await controller.UpdateAsync(
                item.Id,
                new UpdateKnownHostRequest
                {
                    Name = "db-primary",
                    Address = "198.51.100.10",
                    Comment = "primary database",
                    IsVisible = false,
                },
                cancellationToken);
            OkObjectResult updateOk = Assert.IsInstanceOfType<OkObjectResult>(updateResult);
            KnownHostInventoryResponse updated = Assert.IsInstanceOfType<KnownHostInventoryResponse>(updateOk.Value);
            KnownHostItem updatedItem = updated.Hosts.Single();
            Assert.AreEqual(item.Id, updatedItem.Id);
            Assert.AreEqual("198.51.100.10", updatedItem.Address);
            Assert.IsFalse(updatedItem.IsVisible);

            ApplicationDbContext context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            context.ChangeTracker.Clear();
            KnownHostEntry persisted = await context.Set<KnownHostEntry>().SingleAsync(cancellationToken);
            Assert.AreEqual("DB-PRIMARY", persisted.NormalizedName);
            Assert.AreEqual(item.Id, persisted.PublicId);

            IActionResult deleteResult = await controller.DeleteAsync(item.Id, cancellationToken);
            OkObjectResult deleteOk = Assert.IsInstanceOfType<OkObjectResult>(deleteResult);
            KnownHostInventoryResponse deleted = Assert.IsInstanceOfType<KnownHostInventoryResponse>(deleteOk.Value);
            Assert.IsEmpty(deleted.Hosts);
            context.ChangeTracker.Clear();
            Assert.IsFalse(await context.Set<KnownHostEntry>().AnyAsync(cancellationToken));
        }, TestContext.CancellationToken);
}
