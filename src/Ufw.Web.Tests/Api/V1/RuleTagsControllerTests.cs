using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Data.Access;
using Ufw.Web.Data.Access.Rules.Tags;
using Ufw.Web.Model.V1.RuleTags;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class RuleTagsControllerTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task GetAsync_WrapsDomainInventoryInResponseAsync()
    {
        IReadOnlyList<RuleTagItem> expected = [new RuleTagItem(Guid.CreateVersion7(), "Production", "#12AB34")];
        Mock<IRuleTagDataAccess> data = new();
        data.Setup(candidate => candidate.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        RuleTagsController controller = new(data.Object);

        ActionResult<RuleTagInventoryResponse> action = await controller.GetAsync(TestContext.CancellationToken);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(action.Result);
        RuleTagInventoryResponse response = Assert.IsInstanceOfType<RuleTagInventoryResponse>(ok.Value);
        Assert.AreSame(expected, response.Tags);
    }

    [TestMethod]
    public async Task CreateAsync_NormalizesRequestAndReadsInventoryAfterSuccessfulMutationAsync()
    {
        Guid id = Guid.CreateVersion7();
        IReadOnlyList<RuleTagItem> inventory = [new RuleTagItem(id, "Production", "#12AB34")];
        Mock<IRuleTagDataAccess> data = new();
        data.Setup(candidate => candidate.CreateAsync("Production", "#12AB34", It.IsAny<CancellationToken>())).ReturnsAsync(DataMutationResult.Success());
        data.Setup(candidate => candidate.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(inventory);
        RuleTagsController controller = new(data.Object);

        IActionResult action = await controller.CreateAsync(
            new CreateRuleTagRequest { Name = "  Production  ", Color = "  #12ab34  " },
            TestContext.CancellationToken);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(action);
        RuleTagInventoryResponse response = Assert.IsInstanceOfType<RuleTagInventoryResponse>(ok.Value);
        Assert.AreSame(inventory, response.Tags);
        data.Verify(candidate => candidate.CreateAsync("Production", "#12AB34", It.IsAny<CancellationToken>()), Times.Once);
        data.Verify(candidate => candidate.GetAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task UpdateAsync_UniqueConflictReturnsConflictWithoutInventoryReadAsync()
    {
        Guid id = Guid.CreateVersion7();
        Mock<IRuleTagDataAccess> data = new();
        data.Setup(candidate => candidate.UpdateAsync(id, "Production", "#12AB34", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataMutationResult.Failure(new DataMutationUniqueConflictError()));
        RuleTagsController controller = new(data.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        IActionResult action = await controller.UpdateAsync(
            id,
            new UpdateRuleTagRequest { Name = " Production ", Color = " #12ab34 " },
            TestContext.CancellationToken);

        ConflictObjectResult conflict = Assert.IsInstanceOfType<ConflictObjectResult>(action);
        ProblemDetails problem = Assert.IsInstanceOfType<ProblemDetails>(conflict.Value);
        Assert.AreEqual(StatusCodes.Status409Conflict, problem.Status);
        data.Verify(candidate => candidate.GetAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task DeleteAsync_ReferenceConflictReturnsConflictWithoutInventoryReadAsync()
    {
        Guid id = Guid.CreateVersion7();
        Mock<IRuleTagDataAccess> data = new();
        data.Setup(candidate => candidate.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataMutationResult.Failure(new DataMutationReferenceConflictError()));
        RuleTagsController controller = new(data.Object)
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
        CreateRuleTagRequest request = new()
        {
            Name = " ",
            Color = "#12345G",
        };

        List<ValidationResult> errors = [];
        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);

        Assert.IsFalse(valid);
        Assert.HasCount(2, errors);
    }

    [TestMethod]
    public void UpdateRequest_AcceptsValuesAtSharedLimits()
    {
        UpdateRuleTagRequest request = new()
        {
            Name = new string('n', RuleTagLimits.MAX_NAME_LENGTH),
            Color = "#a1B2c3",
        };

        List<ValidationResult> errors = [];
        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);

        Assert.IsTrue(valid);
        Assert.IsEmpty(errors);
    }
}
