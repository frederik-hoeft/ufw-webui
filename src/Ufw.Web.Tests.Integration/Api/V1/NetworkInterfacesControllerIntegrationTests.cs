using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ufw.Shared.Management.NetworkInterfaces;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Data;
using Ufw.Web.Data.Model;
using Ufw.Web.Model.V1.NetworkInterfaces;
using Ufw.Web.Tests.Integration.Support;

namespace Ufw.Web.Tests.Integration.Api.V1;

[TestClass]
internal sealed class NetworkInterfacesControllerIntegrationTests : ControllerIntegrationTest<NetworkInterfacesController>
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public Task ReconcileAndUpdateMetadataAsync_PersistsThroughControllerServiceAndDataAccessLayersAsync() =>
        UsingComponentAsync(async (controller, serviceProvider, cancellationToken) =>
        {
            IntegrationNetworkInterfaceDaemonGateway daemonSource = serviceProvider.GetRequiredService<IntegrationNetworkInterfaceDaemonGateway>();
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
    [TestMethod]
    public Task CleanupStaleAsync_RevalidatesPresenceBeforePermanentDeletionAsync() =>
        UsingComponentAsync(async (controller, serviceProvider, cancellationToken) =>
        {
            IntegrationNetworkInterfaceDaemonGateway daemon = serviceProvider.GetRequiredService<IntegrationNetworkInterfaceDaemonGateway>();
            daemon.SetInterfaceNames("eno1");
            IActionResult reconcileResult = await controller.ReconcileAsync(cancellationToken);
            OkObjectResult reconcileOk = Assert.IsInstanceOfType<OkObjectResult>(reconcileResult);
            NetworkInterfaceInventoryResponse initial = Assert.IsInstanceOfType<NetworkInterfaceInventoryResponse>(reconcileOk.Value);
            NetworkInterfaceInventoryItem eno1 = initial.Interfaces.Single();

            IActionResult commentResult = await controller.UpdateCommentAsync(
                eno1.Id,
                new UpdateNetworkInterfaceCommentRequest { Comment = "retain me" },
                cancellationToken);
            Assert.IsInstanceOfType<OkObjectResult>(commentResult);

            daemon.SetInterfaceNames();
            _ = await controller.ReconcileAsync(cancellationToken);
            ActionResult<NetworkInterfaceCleanupResponse> staleResult = await controller.GetStaleAsync(cancellationToken);
            OkObjectResult staleOk = Assert.IsInstanceOfType<OkObjectResult>(staleResult.Result);
            NetworkInterfaceCleanupResponse stale = Assert.IsInstanceOfType<NetworkInterfaceCleanupResponse>(staleOk.Value);
            Assert.HasCount(1, stale.StaleInterfaces);
            Assert.AreEqual(eno1.Id, stale.StaleInterfaces[0].Id);

            daemon.SetInterfaceNames("eno1");
            ActionResult<NetworkInterfaceCleanupResponse> cleanupResult = await controller.CleanupStaleAsync(
                new CleanupNetworkInterfacesRequest { InterfaceIds = [eno1.Id] },
                cancellationToken);
            OkObjectResult cleanupOk = Assert.IsInstanceOfType<OkObjectResult>(cleanupResult.Result);
            NetworkInterfaceCleanupResponse cleanup = Assert.IsInstanceOfType<NetworkInterfaceCleanupResponse>(cleanupOk.Value);
            Assert.AreEqual(0, cleanup.RemovedCount);
            Assert.IsEmpty(cleanup.StaleInterfaces);

            ActionResult<NetworkInterfaceInventoryResponse> currentResult = await controller.GetAsync(cancellationToken);
            OkObjectResult currentOk = Assert.IsInstanceOfType<OkObjectResult>(currentResult.Result);
            NetworkInterfaceInventoryResponse current = Assert.IsInstanceOfType<NetworkInterfaceInventoryResponse>(currentOk.Value);
            Assert.AreEqual(eno1.Id, current.Interfaces.Single().Id);
            Assert.AreEqual("retain me", current.Interfaces.Single().Comment);
        }, TestContext.CancellationToken);

}
