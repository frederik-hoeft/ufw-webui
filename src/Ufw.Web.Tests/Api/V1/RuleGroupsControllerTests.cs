using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Model.V1.RuleGroups;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class RuleGroupsControllerTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task GetAsync_ReturnsServiceInventoryAsync()
    {
        RuleGroupInventoryResponse expected = new([new RuleGroupItem(Guid.CreateVersion7(), "Core", "comment", ["sha256:rule"]) ]);
        Mock<IRuleGroupService> service = new();
        service.Setup(candidate => candidate.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        RuleGroupsController controller = new(service.Object);

        ActionResult<RuleGroupInventoryResponse> action = await controller.GetAsync(TestContext.CancellationToken);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(action.Result);
        Assert.AreSame(expected, ok.Value);
    }

    [TestMethod]
    public async Task CreateAsync_InvalidGroupReturnsBadRequestAsync()
    {
        Mock<IRuleGroupService> service = new();
        CreateRuleGroupRequest request = new() { Name = " " };
        service.Setup(candidate => candidate.CreateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleGroupMutationResult(RuleGroupMutationOutcome.InvalidGroup));
        RuleGroupsController controller = new(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        IActionResult action = await controller.CreateAsync(request, TestContext.CancellationToken);

        BadRequestObjectResult badRequest = Assert.IsInstanceOfType<BadRequestObjectResult>(action);
        ProblemDetails problem = Assert.IsInstanceOfType<ProblemDetails>(badRequest.Value);
        Assert.AreEqual(StatusCodes.Status400BadRequest, problem.Status);
    }

    [TestMethod]
    public async Task UpdateAsync_NameConflictReturnsConflictAsync()
    {
        Guid id = Guid.CreateVersion7();
        Mock<IRuleGroupService> service = new();
        UpdateRuleGroupRequest request = new() { Name = "Core" };
        service.Setup(candidate => candidate.UpdateAsync(id, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleGroupMutationResult(RuleGroupMutationOutcome.NameConflict));
        RuleGroupsController controller = new(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        IActionResult action = await controller.UpdateAsync(id, request, TestContext.CancellationToken);

        ConflictObjectResult conflict = Assert.IsInstanceOfType<ConflictObjectResult>(action);
        ProblemDetails problem = Assert.IsInstanceOfType<ProblemDetails>(conflict.Value);
        Assert.AreEqual(StatusCodes.Status409Conflict, problem.Status);
    }

    [TestMethod]
    public async Task DeleteAsync_InUseReturnsConflictAsync()
    {
        Guid id = Guid.CreateVersion7();
        Mock<IRuleGroupService> service = new();
        service.Setup(candidate => candidate.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleGroupMutationResult(RuleGroupMutationOutcome.InUse));
        RuleGroupsController controller = new(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        IActionResult action = await controller.DeleteAsync(id, TestContext.CancellationToken);

        ConflictObjectResult conflict = Assert.IsInstanceOfType<ConflictObjectResult>(action);
        ProblemDetails problem = Assert.IsInstanceOfType<ProblemDetails>(conflict.Value);
        Assert.AreEqual(StatusCodes.Status409Conflict, problem.Status);
    }
}
