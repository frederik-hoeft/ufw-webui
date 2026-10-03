using Ufw.Shared.Management.KnownHosts;
using System.ComponentModel.DataAnnotations;
﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Model.V1.KnownHosts;
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
    public async Task ReconcileDnsAsync_ResolutionFailure_ReturnsUnprocessableEntityAsync()
    {
        Mock<IKnownHostService> service = new();
        Guid id = Guid.CreateVersion7();
        service.Setup(candidate => candidate.ReconcileDnsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KnownHostMutationResult(KnownHostMutationOutcome.DnsResolutionFailed));
        KnownHostsController controller = new(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        IActionResult result = await controller.ReconcileDnsAsync(id, TestContext.CancellationToken);

        ObjectResult failure = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status422UnprocessableEntity, failure.StatusCode);
        ProblemDetails problem = Assert.IsInstanceOfType<ProblemDetails>(failure.Value);
        Assert.AreEqual(StatusCodes.Status422UnprocessableEntity, problem.Status);
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
    [TestMethod]
    public void RequestValidation_UsesSharedTrimmedMetadataLimits()
    {
        AssertInvalid(new CreateKnownHostRequest { Name = "   ", Address = "192.0.2.1" });
        AssertInvalid(new CreateKnownHostRequest { Name = new string('n', KnownHostLimits.MAX_NAME_LENGTH + 1), Address = "192.0.2.1" });
        AssertInvalid(new CreateKnownHostRequest
        {
            Name = "host",
            Address = "192.0.2.1",
            Comment = new string('c', KnownHostLimits.MAX_COMMENT_LENGTH + 1),
        });

        AssertValid(new CreateKnownHostRequest
        {
            Name = $"  {new string('n', KnownHostLimits.MAX_NAME_LENGTH)}  ",
            Address = "192.0.2.1",
            Comment = $"  {new string('c', KnownHostLimits.MAX_COMMENT_LENGTH)}  ",
        });
    }

    private static void AssertInvalid(KnownHostRequest request)
    {
        List<ValidationResult> errors = [];
        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);
        Assert.IsFalse(valid);
        Assert.IsNotEmpty(errors);
    }

    private static void AssertValid(KnownHostRequest request)
    {
        List<ValidationResult> errors = [];
        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);
        Assert.IsTrue(valid);
        Assert.IsEmpty(errors);
    }

}
