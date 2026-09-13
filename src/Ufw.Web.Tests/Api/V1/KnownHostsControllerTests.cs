using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Api.V1.Models.KnownHosts;
using Ufw.Web.Services.KnownHosts;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class KnownHostsControllerTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task UpdateAsync_AddressFamilyConflict_ReturnsConflictAsync()
    {
        Mock<IKnownHostService> service = new();
        Guid id = Guid.CreateVersion7();
        UpdateKnownHostRequest request = new() { Name = "router", Address = "2001:db8::1" };
        service.Setup(candidate => candidate.UpdateAsync(id, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnownHostMutationResult(KnownHostMutationOutcome.AddressFamilyConflict));
        KnownHostsController controller = new(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        IActionResult result = await controller.UpdateAsync(id, request, TestContext.CancellationToken);

        ObjectResult conflict = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status409Conflict, conflict.StatusCode);
        ProblemDetails problem = Assert.IsInstanceOfType<ProblemDetails>(conflict.Value);
        Assert.AreEqual(StatusCodes.Status409Conflict, problem.Status);
    }

    [TestMethod]
    public async Task DeleteAsync_NotFound_ReturnsNotFoundAsync()
    {
        Mock<IKnownHostService> service = new();
        Guid id = Guid.CreateVersion7();
        service.Setup(candidate => candidate.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnownHostMutationResult(KnownHostMutationOutcome.NotFound));
        KnownHostsController controller = new(service.Object);

        IActionResult result = await controller.DeleteAsync(id, TestContext.CancellationToken);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }
}
