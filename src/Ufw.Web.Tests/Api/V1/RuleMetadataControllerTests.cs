using Microsoft.AspNetCore.Mvc;
using Moq;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Api.V1.Errors;
using Ufw.Web.Api.V1.Models.Rules;
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
    public async Task CleanupAsync_InvalidSelectionReturnsBadRequestWithoutInvokingServiceAsync()
    {
        Mock<IRuleMetadataReconciliationService> service = new(MockBehavior.Strict);
        RuleMetadataController controller = CreateController(service.Object);

        ActionResult<RuleMetadataReconciliationResponse> action = await controller.CleanupAsync(
            new CleanupRuleMetadataRequest(),
            CancellationToken.None);

        Assert.IsInstanceOfType<BadRequestObjectResult>(action.Result);
        service.VerifyNoOtherCalls();
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

    private static RuleMetadataController CreateController(IRuleMetadataReconciliationService service)
    {
        Mock<IDaemonApiErrorMapper> errors = new();
        return new RuleMetadataController(service, errors.Object);
    }
}
