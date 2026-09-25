using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ufw.Shared.Firewall;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Model.V1.KnownHosts;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Ufw.Web.Tests.Integration.Support;

namespace Ufw.Web.Tests.Integration.Api.V1;

[TestClass]
public sealed class KnownHostsControllerIntegrationTests : ControllerIntegrationTest<KnownHostsController>
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public Task DnsBackedAliasAsync_ResolvesPersistsAndReconcilesWithoutChangingAliasIdentityAsync() =>
        UsingComponentAsync(async (controller, serviceProvider, cancellationToken) =>
        {
            IntegrationKnownHostDnsResolver resolver = serviceProvider.GetRequiredService<IntegrationKnownHostDnsResolver>();
            IntegrationTimeProvider timeProvider = serviceProvider.GetRequiredService<IntegrationTimeProvider>();
            DateTimeOffset createdAt = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
            DateTimeOffset reconciledAt = createdAt.AddHours(2);
            timeProvider.SetUtcNow(createdAt);
            resolver.SetResult("nas.example.test", FirewallAddressFamily.IPv4, "192.0.2.10");

            IActionResult createResult = await controller.CreateAsync(
                new CreateKnownHostRequest
                {
                    Name = "nas.example.test",
                    AddressSource = KnownHostAddressSource.Dns,
                    DnsAddressFamily = FirewallAddressFamily.IPv4,
                    Comment = "storage",
                    IsVisible = true,
                },
                cancellationToken);
            OkObjectResult createOk = Assert.IsInstanceOfType<OkObjectResult>(createResult);
            KnownHostInventoryItem created = Assert.IsInstanceOfType<KnownHostInventoryResponse>(createOk.Value).Hosts.Single();
            Assert.AreEqual(KnownHostAddressSource.Dns, created.AddressSource);
            Assert.AreEqual("192.0.2.10", created.Address);
            Assert.AreEqual(createdAt, created.DnsResolvedAt);

            timeProvider.SetUtcNow(reconciledAt);
            resolver.SetResult("nas.example.test", FirewallAddressFamily.IPv4, "192.0.2.20");
            IActionResult reconcileResult = await controller.ReconcileDnsAsync(created.Id, cancellationToken);
            OkObjectResult reconcileOk = Assert.IsInstanceOfType<OkObjectResult>(reconcileResult);
            KnownHostInventoryItem reconciled = Assert.IsInstanceOfType<KnownHostInventoryResponse>(reconcileOk.Value).Hosts.Single();
            Assert.AreEqual(created.Id, reconciled.Id);
            Assert.AreEqual(created.Name, reconciled.Name);
            Assert.AreEqual("192.0.2.20", reconciled.Address);
            Assert.AreEqual(reconciledAt, reconciled.DnsResolvedAt);

            ApplicationDbContext context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            context.ChangeTracker.Clear();
            KnownHostEntry persisted = await context.Set<KnownHostEntry>().SingleAsync(cancellationToken);
            Assert.AreEqual(KnownHostAddressSource.Dns, persisted.AddressSource);
            Assert.AreEqual("192.0.2.20", persisted.Address);
            Assert.AreEqual(reconciledAt, persisted.DnsResolvedAt);
        }, TestContext.CancellationToken);

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
            KnownHostInventoryItem item = created.Hosts.Single();
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
            KnownHostInventoryItem updatedItem = updated.Hosts.Single();
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
