using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ufw.Shared.Firewall;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Model.V1.NetworkInterfaces;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Model.V1.RuleTemplates;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class RequestValidationTests
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

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
    public void TagIds_RequiresTransportPresenceButAllowsExplicitEmptyCollection()
    {
        AssertTagIdsContract(new UpdateRuleMetadataRequest { TagIds = [] });
        AssertTagIdsContract(new CreateRuleTemplateRequest
        {
            Name = "Template",
            Rule = new FirewallRuleSpecification
            {
                Action = FirewallAction.Allow,
                AddressFamily = FirewallAddressFamily.IPv4,
                Direction = FirewallDirection.In,
                Protocol = FirewallProtocol.Tcp,
                Source = RuleSpecificationNormalizer.ANY,
                Destination = RuleSpecificationNormalizer.ANY,
            },
            TagIds = [],
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

    private static void AssertTagIdsContract<TRequest>(TRequest validRequest) where TRequest : class
    {
        JsonObject json = Assert.IsInstanceOfType<JsonObject>(JsonSerializer.SerializeToNode(validRequest, s_jsonOptions));
        Assert.IsTrue(json.Remove("tagIds"));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<TRequest>(json.ToJsonString(), s_jsonOptions));

        json["tagIds"] = null;
        TRequest? nullTags = JsonSerializer.Deserialize<TRequest>(json.ToJsonString(), s_jsonOptions);
        Assert.IsNotNull(nullTags);
        AssertInvalid(nullTags);

        json["tagIds"] = new JsonArray();
        TRequest? emptyTags = JsonSerializer.Deserialize<TRequest>(json.ToJsonString(), s_jsonOptions);
        Assert.IsNotNull(emptyTags);
        AssertValid(emptyTags);
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
