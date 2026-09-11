using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Api.V1.Models.NetworkInterfaces;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Ufw.Web.Tests.Integration.Support;

namespace Ufw.Web.Tests.Integration.Api.V1;

[TestClass]
public sealed class NetworkInterfacesControllerIntegrationTests : ComponentIntegrationTest<NetworkInterfacesController>
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public Task ReconcileAndUpdateMetadataAsync_PersistsThroughControllerServiceAndRepositoryLayersAsync() =>
        UsingComponentAsync(async (controller, serviceProvider, cancellationToken) =>
        {
            IntegrationDaemonNetworkInterfaceSource daemonSource = serviceProvider.GetRequiredService<IntegrationDaemonNetworkInterfaceSource>();
            IntegrationTimeProvider timeProvider = serviceProvider.GetRequiredService<IntegrationTimeProvider>();
            daemonSource.SetInterfaceNames("eno1", "enp4s0f2.1100");
            DateTimeOffset reconciledAt = new(2026, 9, 11, 16, 30, 0, TimeSpan.Zero);
            timeProvider.SetUtcNow(reconciledAt);

            IActionResult reconcileResult = await controller.ReconcileAsync(cancellationToken);
            OkObjectResult reconcileOk = Assert.IsInstanceOfType<OkObjectResult>(reconcileResult);
            NetworkInterfaceInventoryResponse reconciled = Assert.IsInstanceOfType<NetworkInterfaceInventoryResponse>(reconcileOk.Value);
            Assert.AreEqual(reconciledAt, reconciled.ReconciledAt);
            Assert.HasCount(2, reconciled.Interfaces);

            NetworkInterfaceInventoryItem eno1 = reconciled.Interfaces.Single(static item => item.Name == "eno1");
            IActionResult commentResult = await controller.UpdateCommentAsync(
                eno1.Id,
                new UpdateNetworkInterfaceCommentRequest { Comment = "  management VLAN  " },
                cancellationToken);
            OkObjectResult commentOk = Assert.IsInstanceOfType<OkObjectResult>(commentResult);
            NetworkInterfaceInventoryResponse commented = Assert.IsInstanceOfType<NetworkInterfaceInventoryResponse>(commentOk.Value);
            Assert.AreEqual("management VLAN", commented.Interfaces.Single(static item => item.Name == "eno1").Comment);

            IActionResult visibilityResult = await controller.UpdateVisibilityAsync(
                eno1.Id,
                new UpdateNetworkInterfaceVisibilityRequest { IsVisible = false },
                cancellationToken);
            Assert.IsInstanceOfType<OkObjectResult>(visibilityResult);

            ApplicationDbContext context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            context.ChangeTracker.Clear();
            NetworkInterfaceEntry persisted = await context.Set<NetworkInterfaceEntry>()
                .SingleAsync(static entry => entry.Name == "eno1", cancellationToken);
            Assert.AreEqual(eno1.Id, persisted.PublicId);
            Assert.AreEqual("management VLAN", persisted.Comment);
            Assert.IsFalse(persisted.IsVisible);

            NetworkInterfaceCacheState cacheState = await context.Set<NetworkInterfaceCacheState>()
                .SingleAsync(cancellationToken);
            Assert.AreEqual(reconciledAt, cacheState.ReconciledAt);
        }, TestContext.CancellationToken);
}
