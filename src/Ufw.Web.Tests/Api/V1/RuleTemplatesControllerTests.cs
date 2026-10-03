using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Ufw.Shared.Firewall;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Data.Access;
using Ufw.Web.Data.Access.Rules;
using Ufw.Web.Data.Access.Rules.Templates;
using Ufw.Web.Model.V1.RuleTemplates;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class RuleTemplatesControllerTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task GetAsync_ReturnsDataAccessInventoryAsync()
    {
        RuleTemplateItem template = new() { Id = Guid.CreateVersion7(), Name = "Template", Rule = ValidRule() };
        Mock<IRuleTemplateDataAccess> dataAccess = new();
        dataAccess.Setup(candidate => candidate.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync([template]);
        RuleTemplatesController controller = CreateController(dataAccess.Object);

        ActionResult<RuleTemplateInventoryResponse> action = await controller.GetAsync(TestContext.CancellationToken);

        RuleTemplateInventoryResponse response = Assert.IsInstanceOfType<RuleTemplateInventoryResponse>(Assert.IsInstanceOfType<OkObjectResult>(action.Result).Value);
        CollectionAssert.AreEqual(new[] { template }, response.Templates.ToArray());
    }

    [TestMethod]
    public async Task CreateAsync_NormalizesValidatedRequestBeforeDataAccessAsync()
    {
        Guid firstTag = Guid.Parse("0199a100-0000-7000-8000-000000000001");
        Guid secondTag = Guid.Parse("0199a100-0000-7000-8000-000000000002");
        Guid group = Guid.Parse("0199a100-0000-7000-8000-000000000003");
        RuleTemplateValues? captured = null;
        Mock<IRuleTemplateDataAccess> dataAccess = new();
        dataAccess.Setup(candidate => candidate.CreateAsync(It.IsAny<RuleTemplateValues>(), It.IsAny<CancellationToken>()))
            .Callback<RuleTemplateValues, CancellationToken>((values, _) => captured = values)
            .ReturnsAsync(DataMutationResult.Success());
        dataAccess.Setup(candidate => candidate.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        RuleTemplatesController controller = CreateController(dataAccess.Object);

        IActionResult action = await controller.CreateAsync(new CreateRuleTemplateRequest
        {
            Name = "  Web ingress  ",
            Description = "  reusable ingress  ",
            Rule = new FirewallRuleSpecification
            {
                Action = FirewallAction.Allow,
                AddressFamily = FirewallAddressFamily.Any,
                Direction = FirewallDirection.Forward,
                Protocol = FirewallProtocol.Tcp,
                Source = " 10.0.0.25/24 ",
                SourcePorts = "443,80",
                SourceInterface = " lan0 ",
                Destination = " 192.0.2.25 ",
                DestinationPorts = " 8443 ",
                DestinationInterface = " dmz0 ",
                Comment = "  web ingress  ",
            },
            Notes = "  operations note  ",
            TagIds = [secondTag, firstTag, secondTag],
            GroupId = group,
        }, TestContext.CancellationToken);

        Assert.IsInstanceOfType<OkObjectResult>(action);
        Assert.IsNotNull(captured);
        Assert.AreEqual("Web ingress", captured.Name);
        Assert.AreEqual("reusable ingress", captured.Description);
        Assert.AreEqual(FirewallAddressFamily.IPv4, captured.Rule.AddressFamily);
        Assert.AreEqual("10.0.0.0/24", captured.Rule.Source);
        Assert.AreEqual("80,443", captured.Rule.SourcePorts);
        Assert.AreEqual("lan0", captured.Rule.SourceInterface);
        Assert.AreEqual("192.0.2.25", captured.Rule.Destination);
        Assert.AreEqual("8443", captured.Rule.DestinationPorts);
        Assert.AreEqual("dmz0", captured.Rule.DestinationInterface);
        Assert.AreEqual("web ingress", captured.Rule.Comment);
        Assert.AreEqual("operations note", captured.Notes);
        CollectionAssert.AreEqual(new[] { firstTag, secondTag }, captured.TagIds.ToArray());
        Assert.AreEqual(group, captured.GroupId);
        dataAccess.Verify(candidate => candidate.GetAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task CreateAsync_ContextFreeIpv6AndUnknownInterfaceAreAcceptedAsync()
    {
        RuleTemplateValues? captured = null;
        Mock<IRuleTemplateDataAccess> dataAccess = new();
        dataAccess.Setup(candidate => candidate.CreateAsync(It.IsAny<RuleTemplateValues>(), It.IsAny<CancellationToken>()))
            .Callback<RuleTemplateValues, CancellationToken>((values, _) => captured = values)
            .ReturnsAsync(DataMutationResult.Success());
        dataAccess.Setup(candidate => candidate.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        RuleTemplatesController controller = CreateController(dataAccess.Object);

        IActionResult action = await controller.CreateAsync(new CreateRuleTemplateRequest
        {
            Name = "Future IPv6",
            Rule = new FirewallRuleSpecification
            {
                Action = FirewallAction.Allow,
                AddressFamily = FirewallAddressFamily.IPv6,
                Direction = FirewallDirection.In,
                Protocol = FirewallProtocol.Tcp,
                Source = "2001:db8::/64",
                Destination = "any",
                DestinationPorts = "443",
                DestinationInterface = "future0",
            },
            TagIds = [],
        }, TestContext.CancellationToken);

        Assert.IsInstanceOfType<OkObjectResult>(action);
        Assert.IsNotNull(captured);
        Assert.AreEqual(FirewallAddressFamily.IPv6, captured.Rule.AddressFamily);
        Assert.AreEqual("future0", captured.Rule.DestinationInterface);
    }

    [TestMethod]
    public async Task CreateAsync_InvalidRuleDoesNotReachDataAccessAsync()
    {
        Mock<IRuleTemplateDataAccess> dataAccess = new();
        RuleTemplatesController controller = CreateController(dataAccess.Object);

        IActionResult action = await controller.CreateAsync(new CreateRuleTemplateRequest
        {
            Name = "Invalid",
            Rule = new FirewallRuleSpecification
            {
                Action = FirewallAction.Allow,
                Direction = FirewallDirection.In,
                Protocol = FirewallProtocol.Tcp,
                SourcePorts = "70000",
            },
            TagIds = [],
        }, TestContext.CancellationToken);

        BadRequestObjectResult badRequest = Assert.IsInstanceOfType<BadRequestObjectResult>(action);
        Assert.AreEqual(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        dataAccess.Verify(candidate => candidate.CreateAsync(It.IsAny<RuleTemplateValues>(), It.IsAny<CancellationToken>()), Times.Never);
        dataAccess.Verify(candidate => candidate.GetAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public void RequestValidation_RejectsMalformedTransportShape()
    {
        AssertInvalid(new CreateRuleTemplateRequest { Name = " ", Rule = ValidRule(), TagIds = [] });
        AssertInvalid(new CreateRuleTemplateRequest { Name = new string('x', RuleTemplateLimits.MAX_NAME_LENGTH + 1), Rule = ValidRule(), TagIds = [] });
        AssertInvalid(new CreateRuleTemplateRequest { Name = "Template", Description = new string('x', RuleTemplateLimits.MAX_DESCRIPTION_LENGTH + 1), Rule = ValidRule(), TagIds = [] });
        AssertInvalid(new CreateRuleTemplateRequest { Name = "Template", Rule = ValidRule(), Notes = new string('x', RuleMetadataLimits.MAX_NOTES_LENGTH + 1), TagIds = [] });
        AssertInvalid(new CreateRuleTemplateRequest { Name = "Template", Rule = ValidRule(), TagIds = Enumerable.Repeat(Guid.CreateVersion7(), RuleMetadataLimits.MAX_TAG_COUNT + 1).ToArray() });
        AssertInvalid(new CreateRuleTemplateRequest { Name = "Template", Rule = ValidRule(), TagIds = [Guid.Empty] });
        AssertInvalid(new CreateRuleTemplateRequest { Name = "Template", Rule = ValidRule(), TagIds = [], GroupId = Guid.Empty });
        AssertInvalid(new CreateRuleTemplateRequest { Name = "Template", Rule = null!, TagIds = [] });
        AssertInvalid(new CreateRuleTemplateRequest { Name = "Template", Rule = ValidRule(), TagIds = null! });

        AssertInvalid(new CreateRuleTemplateRequest
        {
            Name = $" {new string('x', RuleTemplateLimits.MAX_NAME_LENGTH)} ",
            Description = $" {new string('x', RuleTemplateLimits.MAX_DESCRIPTION_LENGTH)} ",
            Rule = ValidRule(),
            Notes = $" {new string('x', RuleMetadataLimits.MAX_NOTES_LENGTH)} ",
            TagIds = [],
        });
        AssertValid(new CreateRuleTemplateRequest
        {
            Name = new string('x', RuleTemplateLimits.MAX_NAME_LENGTH),
            Description = new string('x', RuleTemplateLimits.MAX_DESCRIPTION_LENGTH),
            Rule = ValidRule(),
            Notes = new string('x', RuleMetadataLimits.MAX_NOTES_LENGTH),
            TagIds = Enumerable.Repeat(Guid.CreateVersion7(), RuleMetadataLimits.MAX_TAG_COUNT).ToArray(),
            GroupId = Guid.CreateVersion7(),
        });
    }

    [TestMethod]
    public async Task UpdateAsync_MissingDependenciesMapWithoutInventoryReadAsync()
    {
        Guid missingTag = Guid.CreateVersion7();
        Guid missingGroup = Guid.CreateVersion7();
        UpdateRuleTemplateRequest request = ValidUpdateRequest();
        Mock<IRuleTemplateDataAccess> dataAccess = new();
        dataAccess.SetupSequence(candidate => candidate.UpdateAsync(It.IsAny<Guid>(), It.IsAny<RuleTemplateValues>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataMutationResult.Failure(new RuleTagsNotFoundError([missingTag])))
            .ReturnsAsync(DataMutationResult.Failure(new RuleGroupNotFoundError(missingGroup)));
        RuleTemplatesController controller = CreateController(dataAccess.Object);

        IActionResult tagAction = await controller.UpdateAsync(Guid.CreateVersion7(), request, TestContext.CancellationToken);
        IActionResult groupAction = await controller.UpdateAsync(Guid.CreateVersion7(), request, TestContext.CancellationToken);

        Assert.AreEqual(StatusCodes.Status400BadRequest, Assert.IsInstanceOfType<BadRequestObjectResult>(tagAction).StatusCode);
        Assert.AreEqual(StatusCodes.Status400BadRequest, Assert.IsInstanceOfType<BadRequestObjectResult>(groupAction).StatusCode);
        dataAccess.Verify(candidate => candidate.GetAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task UpdateAsync_NotFoundReturnsNotFoundWithoutInventoryReadAsync()
    {
        Guid id = Guid.CreateVersion7();
        Mock<IRuleTemplateDataAccess> dataAccess = new();
        dataAccess.Setup(candidate => candidate.UpdateAsync(id, It.IsAny<RuleTemplateValues>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataMutationResult.Failure(new DataMutationNotFoundError()));
        RuleTemplatesController controller = CreateController(dataAccess.Object);

        IActionResult action = await controller.UpdateAsync(id, ValidUpdateRequest(), TestContext.CancellationToken);

        Assert.IsInstanceOfType<NotFoundResult>(action);
        dataAccess.Verify(candidate => candidate.GetAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task DeleteAsync_SuccessReadsInventoryAfterMutationAsync()
    {
        Guid id = Guid.CreateVersion7();
        RuleTemplateItem template = new() { Id = Guid.CreateVersion7(), Name = "Remaining", Rule = ValidRule() };
        Mock<IRuleTemplateDataAccess> dataAccess = new();
        dataAccess.Setup(candidate => candidate.DeleteAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(DataMutationResult.Success());
        dataAccess.Setup(candidate => candidate.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync([template]);
        RuleTemplatesController controller = CreateController(dataAccess.Object);

        IActionResult action = await controller.DeleteAsync(id, TestContext.CancellationToken);

        RuleTemplateInventoryResponse response = Assert.IsInstanceOfType<RuleTemplateInventoryResponse>(Assert.IsInstanceOfType<OkObjectResult>(action).Value);
        CollectionAssert.AreEqual(new[] { template }, response.Templates.ToArray());
    }

    private static RuleTemplatesController CreateController(IRuleTemplateDataAccess dataAccess) => new(dataAccess)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
    };

    private static UpdateRuleTemplateRequest ValidUpdateRequest() => new()
    {
        Name = "Template",
        Rule = ValidRule(),
        TagIds = [],
    };

    private static FirewallRuleSpecification ValidRule() => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.Any,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        Source = "any",
        Destination = "any",
        DestinationPorts = "443",
    };

    private static void AssertValid(RuleTemplateRequest request)
    {
        List<ValidationResult> errors = [];
        Assert.IsTrue(Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true), string.Join(Environment.NewLine, errors));
    }

    private static void AssertInvalid(RuleTemplateRequest request)
    {
        List<ValidationResult> errors = [];
        Assert.IsFalse(Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true));
    }
}
