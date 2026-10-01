using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Model.V1.RuleTemplates;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class RuleTemplatesControllerTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task GetAsync_ReturnsServiceInventoryAsync()
    {
        RuleTemplateInventoryResponse expected = new();
        Mock<IRuleTemplateService> service = new();
        service.Setup(candidate => candidate.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        RuleTemplatesController controller = new(service.Object);

        ActionResult<RuleTemplateInventoryResponse> action = await controller.GetAsync(TestContext.CancellationToken);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(action.Result);
        Assert.AreSame(expected, ok.Value);
    }

    [TestMethod]
    [DataRow(RuleTemplateMutationOutcome.InvalidTemplate, StatusCodes.Status400BadRequest)]
    [DataRow(RuleTemplateMutationOutcome.TagNotFound, StatusCodes.Status400BadRequest)]
    [DataRow(RuleTemplateMutationOutcome.GroupNotFound, StatusCodes.Status400BadRequest)]
    public async Task CreateAsync_MapsFailureOutcomeAsync(RuleTemplateMutationOutcome outcome, int expectedStatus)
    {
        CreateRuleTemplateRequest request = new() { Name = "Template" };
        Mock<IRuleTemplateService> service = new();
        service.Setup(candidate => candidate.CreateAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(new RuleTemplateMutationResult(outcome));
        RuleTemplatesController controller = CreateController(service.Object);

        IActionResult action = await controller.CreateAsync(request, TestContext.CancellationToken);

        ObjectResult result = Assert.IsInstanceOfType<ObjectResult>(action);
        Assert.AreEqual(expectedStatus, result.StatusCode);
    }

    [TestMethod]
    public async Task UpdateAsync_NotFoundReturnsNotFoundAsync()
    {
        Guid id = Guid.CreateVersion7();
        UpdateRuleTemplateRequest request = new() { Name = "Template" };
        Mock<IRuleTemplateService> service = new();
        service.Setup(candidate => candidate.UpdateAsync(id, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleTemplateMutationResult(RuleTemplateMutationOutcome.NotFound));
        RuleTemplatesController controller = CreateController(service.Object);

        IActionResult action = await controller.UpdateAsync(id, request, TestContext.CancellationToken);

        Assert.IsInstanceOfType<NotFoundResult>(action);
    }

    [TestMethod]
    public async Task DeleteAsync_SuccessReturnsInventoryAsync()
    {
        Guid id = Guid.CreateVersion7();
        RuleTemplateInventoryResponse inventory = new();
        Mock<IRuleTemplateService> service = new();
        service.Setup(candidate => candidate.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleTemplateMutationResult(RuleTemplateMutationOutcome.Success, inventory));
        RuleTemplatesController controller = CreateController(service.Object);

        IActionResult action = await controller.DeleteAsync(id, TestContext.CancellationToken);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(action);
        Assert.AreSame(inventory, ok.Value);
    }

    private static RuleTemplatesController CreateController(IRuleTemplateService service) => new(service)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
    };
}
