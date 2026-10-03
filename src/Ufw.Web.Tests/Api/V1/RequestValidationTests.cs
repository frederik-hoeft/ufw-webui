using System.ComponentModel.DataAnnotations;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Model.V1.NetworkInterfaces;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class RequestValidationTests
{
    [TestMethod]
    public void RuleMetadataUpdate_RejectsTransportShapeAndValidatesRawStringLength()
    {
        AssertInvalid(new UpdateRuleMetadataRequest { TagIds = null! });
        AssertInvalid(new UpdateRuleMetadataRequest { TagIds = [Guid.Empty] });
        AssertInvalid(new UpdateRuleMetadataRequest { TagIds = Enumerable.Repeat(Guid.CreateVersion7(), RuleMetadataLimits.MAX_TAG_COUNT + 1).ToArray() });
        AssertInvalid(new UpdateRuleMetadataRequest { GroupId = Guid.Empty });
        AssertInvalid(new UpdateRuleMetadataRequest { Notes = new string('n', RuleMetadataLimits.MAX_NOTES_LENGTH + 1) });

        AssertInvalid(new UpdateRuleMetadataRequest
        {
            Notes = $" {new string('n', RuleMetadataLimits.MAX_NOTES_LENGTH)} ",
            TagIds = [Guid.CreateVersion7()],
            GroupId = Guid.CreateVersion7(),
        });
        AssertValid(new UpdateRuleMetadataRequest
        {
            Notes = new string('n', RuleMetadataLimits.MAX_NOTES_LENGTH),
            TagIds = [Guid.CreateVersion7()],
            GroupId = Guid.CreateVersion7(),
        });
    }

    [TestMethod]
    public void NetworkInterfaceCleanup_RejectsMissingOrEmptyIdentities()
    {
        AssertInvalid(new CleanupNetworkInterfacesRequest { InterfaceIds = null! });
        AssertInvalid(new CleanupNetworkInterfacesRequest());
        AssertInvalid(new CleanupNetworkInterfacesRequest { InterfaceIds = [Guid.Empty] });
        AssertValid(new CleanupNetworkInterfacesRequest { InterfaceIds = [Guid.CreateVersion7()] });
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
}
