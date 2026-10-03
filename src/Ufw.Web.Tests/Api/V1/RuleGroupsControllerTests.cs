using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Data.Access;
using Ufw.Web.Data.Access.Rules.Groups;
using Ufw.Web.Model.V1.RuleGroups;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class RuleGroupsControllerTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task GetAsync_WrapsDomainInventoryInResponseAsync()
    {
        IReadOnlyList<RuleGroupItem> expected = [new RuleGroupItem(Guid.CreateVersion7(), "Core", "comment", ["sha256:rule"])];
        Mock<IRuleGroupDataAccess> data = new();
        data.Setup(candidate => candidate.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        RuleGroupsController controller = new(data.Object);

        ActionResult<RuleGroupInventoryResponse> action = await controller.GetAsync(TestContext.CancellationToken);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(action.Result);
        RuleGroupInventoryResponse response = Assert.IsInstanceOfType<RuleGroupInventoryResponse>(ok.Value);
        Assert.AreSame(expected, response.Groups);
    }

    [TestMethod]
    public async Task CreateAsync_NormalizesRequestAndReadsInventoryAfterSuccessfulMutationAsync()
    {
        Guid id = Guid.CreateVersion7();
        IReadOnlyList<RuleGroupItem> inventory = [new RuleGroupItem(id, "Core", "managed", [])];
        Mock<IRuleGroupDataAccess> data = new();
        data.Setup(candidate => candidate.CreateAsync("Core", "managed", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataMutationResult.Success());
        data.Setup(candidate => candidate.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(inventory);
        RuleGroupsController controller = new(data.Object);

        IActionResult action = await controller.CreateAsync(
            new CreateRuleGroupRequest { Name = "  Core  ", Comment = "  managed  " },
            TestContext.CancellationToken);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(action);
        RuleGroupInventoryResponse response = Assert.IsInstanceOfType<RuleGroupInventoryResponse>(ok.Value);
        Assert.AreSame(inventory, response.Groups);
        data.Verify(candidate => candidate.CreateAsync("Core", "managed", It.IsAny<CancellationToken>()), Times.Once);
        data.Verify(candidate => candidate.GetAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task UpdateAsync_NameConflictReturnsConflictWithoutInventoryReadAsync()
    {
        Guid id = Guid.CreateVersion7();
        Mock<IRuleGroupDataAccess> data = new();
        data.Setup(candidate => candidate.UpdateAsync(id, "Core", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataMutationResult.Failure(new DataMutationUniqueConflictError()));
        RuleGroupsController controller = new(data.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        IActionResult action = await controller.UpdateAsync(id, new UpdateRuleGroupRequest { Name = " Core ", Comment = "  " }, TestContext.CancellationToken);

        ConflictObjectResult conflict = Assert.IsInstanceOfType<ConflictObjectResult>(action);
        ProblemDetails problem = Assert.IsInstanceOfType<ProblemDetails>(conflict.Value);
        Assert.AreEqual(StatusCodes.Status409Conflict, problem.Status);
        data.Verify(candidate => candidate.GetAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task DeleteAsync_InUseReturnsConflictWithoutInventoryReadAsync()
    {
        Guid id = Guid.CreateVersion7();
        Mock<IRuleGroupDataAccess> data = new();
        data.Setup(candidate => candidate.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataMutationResult.Failure(new DataMutationReferenceConflictError()));
        RuleGroupsController controller = new(data.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        IActionResult action = await controller.DeleteAsync(id, TestContext.CancellationToken);

        ConflictObjectResult conflict = Assert.IsInstanceOfType<ConflictObjectResult>(action);
        ProblemDetails problem = Assert.IsInstanceOfType<ProblemDetails>(conflict.Value);
        Assert.AreEqual(StatusCodes.Status409Conflict, problem.Status);
        data.Verify(candidate => candidate.GetAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public void CreateRequest_RejectsInvalidTransportShape()
    {
        CreateRuleGroupRequest request = new()
        {
            Name = " ",
            Comment = new string('x', RuleGroupLimits.MAX_COMMENT_LENGTH + 1),
        };

        List<ValidationResult> errors = [];
        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);

        Assert.IsFalse(valid);
        Assert.HasCount(2, errors);
    }

    [TestMethod]
    public void UpdateRequest_ValidatesRawValuesAtSharedLimits()
    {
        UpdateRuleGroupRequest padded = new()
        {
            Name = $" {new string('n', RuleGroupLimits.MAX_NAME_LENGTH)} ",
            Comment = $" {new string('c', RuleGroupLimits.MAX_COMMENT_LENGTH)} ",
        };
        UpdateRuleGroupRequest exact = new()
        {
            Name = new string('n', RuleGroupLimits.MAX_NAME_LENGTH),
            Comment = new string('c', RuleGroupLimits.MAX_COMMENT_LENGTH),
        };

        List<ValidationResult> paddedErrors = [];
        bool paddedValid = Validator.TryValidateObject(padded, new ValidationContext(padded), paddedErrors, validateAllProperties: true);
        List<ValidationResult> exactErrors = [];
        bool exactValid = Validator.TryValidateObject(exact, new ValidationContext(exact), exactErrors, validateAllProperties: true);

        Assert.IsFalse(paddedValid);
        Assert.HasCount(2, paddedErrors);
        Assert.IsTrue(exactValid);
        Assert.IsEmpty(exactErrors);
    }
}
