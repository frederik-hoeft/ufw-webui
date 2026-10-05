using Microsoft.AspNetCore.Mvc;
using Moq;
using System.ComponentModel.DataAnnotations;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Model.V1.RuleMetadata;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Model.Validation;
using Ufw.Web.Services.Rules;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class RuleMetadataControllerTests
{
    [TestMethod]
    public async Task GetReconciliationAsync_ReturnsServiceResponseAsync()
    {
        RuleMetadataReconciliationResponse expected = new([new RuleMetadataItem(Guid.CreateVersion7(), "sha256:old", "note", [])]);
        Mock<IRuleMetadataReconciliationService> service = new();
        service.Setup(candidate => candidate.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        RuleMetadataController controller = CreateController(service.Object);

        ActionResult<RuleMetadataReconciliationResponse> action = await controller.GetReconciliationAsync(CancellationToken.None);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(action.Result);
        Assert.AreSame(expected, ok.Value);
    }

    [TestMethod]
    public void CleanupRequest_RejectsInvalidTransportShape()
    {
        AssertInvalid(new CleanupRuleMetadataRequest());
        AssertInvalid(new CleanupRuleMetadataRequest { MetadataIds = [Guid.Empty] });
        AssertValid(new CleanupRuleMetadataRequest { MetadataIds = [Guid.CreateVersion7()] });
    }

    [TestMethod]
    public async Task CleanupAsync_DelegatesReviewedSelectionAsync()
    {
        Guid metadataId = Guid.CreateVersion7();
        RuleMetadataReconciliationResponse expected = new([], 1);
        Mock<IRuleMetadataReconciliationService> service = new();
        service.Setup(candidate => candidate.CleanupAsync(
                It.Is<CleanupRuleMetadataRequest>(request => request.MetadataIds.SequenceEqual(new[] { metadataId })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        RuleMetadataController controller = CreateController(service.Object);

        ActionResult<RuleMetadataReconciliationResponse> action = await controller.CleanupAsync(
            new CleanupRuleMetadataRequest { MetadataIds = [metadataId] },
            CancellationToken.None);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(action.Result);
        Assert.AreSame(expected, ok.Value);
    }

    private static void AssertInvalid(object request)
    {
        List<ValidationResult> errors = [];
        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);
        Assert.IsFalse(valid);
        Assert.IsNotEmpty(errors);
    }

    private static void AssertValid(object request)
    {
        List<ValidationResult> errors = [];
        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);
        Assert.IsTrue(valid, string.Join(Environment.NewLine, errors));
        Assert.IsEmpty(errors);
    }

    private static RuleMetadataController CreateController(IRuleMetadataReconciliationService service) => new(service);
}
