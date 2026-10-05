using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses;

namespace Ufw.Shared.Tests.Firewall;

[TestClass]
public sealed class RuleSpecificationValidatorTests
{
    [TestMethod]
    public void TestValidate_AcceptsCanonicalRuleFields()
    {
        FirewallRuleSpecification specification = new()
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            Source = "10.0.0.0/8",
            Destination = "192.168.1.10",
            DestinationPorts = "22,80:90",
            DestinationInterface = "eth0",
            Comment = "admin access",
        };

        ModelValidationError[] errors = RuleSpecificationValidator.Validate(specification);

        Assert.HasCount(0, errors);
        Assert.IsTrue(RuleSpecificationValidator.TryValidate(specification, out ModelValidationErrorResponse? response));
        Assert.IsNull(response);
    }

    [TestMethod]
    public void TestValidate_AcceptsNaturalCommentPunctuation()
    {
        FirewallRuleSpecification specification = new()
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            Comment = "client (foo) -> server <bar>",
        };

        Assert.HasCount(0, RuleSpecificationValidator.Validate(specification));
    }

    [TestMethod]
    public void TestValidate_ReportsFieldSpecificSemanticErrors()
    {
        FirewallRuleSpecification specification = new()
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            Source = "2001:db8::1",
            SourcePorts = "65536",
            SourceInterface = "eth0",
            Comment = "unsafe;comment",
        };

        ModelValidationError[] errors = RuleSpecificationValidator.Validate(specification);

        Assert.IsTrue(errors.Any(static error => error.PropertyName == nameof(FirewallRuleSpecification.AddressFamily)));
        Assert.IsTrue(errors.Any(static error => error.PropertyName == nameof(FirewallRuleSpecification.SourcePorts)));
        Assert.IsTrue(errors.Any(static error => error.PropertyName == nameof(FirewallRuleSpecification.SourceInterface)));
        Assert.IsTrue(errors.Any(static error => error.PropertyName == nameof(FirewallRuleSpecification.Comment)));
    }

    [TestMethod]
    public void TestValidate_AssignsStableCodesToValidationRules()
    {
        (FirewallRuleSpecification Specification, string PropertyName, string Code)[] cases =
        [
            (new FirewallRuleSpecification { Action = (FirewallAction)int.MaxValue }, nameof(FirewallRuleSpecification.Action), FirewallRuleValidationErrorCodes.ACTION_UNSUPPORTED),
            (new FirewallRuleSpecification { AddressFamily = (FirewallAddressFamily)int.MaxValue },
                nameof(FirewallRuleSpecification.AddressFamily), FirewallRuleValidationErrorCodes.ADDRESS_FAMILY_UNSUPPORTED),
            (new FirewallRuleSpecification { Direction = (FirewallDirection)int.MaxValue }, nameof(FirewallRuleSpecification.Direction), FirewallRuleValidationErrorCodes.DIRECTION_UNSUPPORTED),
            (new FirewallRuleSpecification { Protocol = (FirewallProtocol)int.MaxValue }, nameof(FirewallRuleSpecification.Protocol), FirewallRuleValidationErrorCodes.PROTOCOL_UNSUPPORTED),
            (new FirewallRuleSpecification { Source = "not-an-address" }, nameof(FirewallRuleSpecification.Source), FirewallRuleValidationErrorCodes.ADDRESS_INVALID),
            (new FirewallRuleSpecification { Source = "fe80::1%3" }, nameof(FirewallRuleSpecification.Source), FirewallRuleValidationErrorCodes.SCOPED_IPV6_UNSUPPORTED),
            (new FirewallRuleSpecification { Source = "10.0.0.1/33" }, nameof(FirewallRuleSpecification.Source), FirewallRuleValidationErrorCodes.IPV4_PREFIX_INVALID),
            (new FirewallRuleSpecification { Source = "2001:db8::1/129" }, nameof(FirewallRuleSpecification.Source), FirewallRuleValidationErrorCodes.IPV6_PREFIX_INVALID),
            (new FirewallRuleSpecification { Source = "10.0.0.1", Destination = "2001:db8::1" },
                nameof(FirewallRuleSpecification.AddressFamily), FirewallRuleValidationErrorCodes.ADDRESS_FAMILIES_MUST_MATCH),
            (new FirewallRuleSpecification { AddressFamily = FirewallAddressFamily.IPv4, Source = "2001:db8::1" },
                nameof(FirewallRuleSpecification.AddressFamily), FirewallRuleValidationErrorCodes.ADDRESS_FAMILY_MISMATCH),
            (new FirewallRuleSpecification { SourcePorts = "not-ports" }, nameof(FirewallRuleSpecification.SourcePorts), FirewallRuleValidationErrorCodes.PORTS_SYNTAX_INVALID),
            (new FirewallRuleSpecification { SourcePorts = "65536" }, nameof(FirewallRuleSpecification.SourcePorts), FirewallRuleValidationErrorCodes.PORTS_OUT_OF_RANGE),
            (new FirewallRuleSpecification { SourcePorts = "1:65536" }, nameof(FirewallRuleSpecification.SourcePorts), FirewallRuleValidationErrorCodes.PORT_RANGE_OUT_OF_RANGE),
            (new FirewallRuleSpecification { SourcePorts = "2:1" }, nameof(FirewallRuleSpecification.SourcePorts), FirewallRuleValidationErrorCodes.PORT_RANGE_REVERSED),
            (new FirewallRuleSpecification { Direction = FirewallDirection.Out, SourceInterface = "bad interface" },
                nameof(FirewallRuleSpecification.SourceInterface), FirewallRuleValidationErrorCodes.INTERFACE_INVALID),
            (new FirewallRuleSpecification { Direction = FirewallDirection.In, SourceInterface = "eth0" },
                nameof(FirewallRuleSpecification.SourceInterface), FirewallRuleValidationErrorCodes.INBOUND_SOURCE_INTERFACE_INVALID),
            (new FirewallRuleSpecification { Direction = FirewallDirection.Out, DestinationInterface = "eth0" },
                nameof(FirewallRuleSpecification.DestinationInterface), FirewallRuleValidationErrorCodes.OUTBOUND_DESTINATION_INTERFACE_INVALID),
            (new FirewallRuleSpecification { Comment = "unsafe;comment" }, nameof(FirewallRuleSpecification.Comment), FirewallRuleValidationErrorCodes.COMMENT_INVALID),
        ];

        foreach ((FirewallRuleSpecification specification, string propertyName, string code) in cases)
        {
            ModelValidationError[] errors = RuleSpecificationValidator.Validate(specification);
            Assert.IsTrue(errors.Any(error => error.PropertyName == propertyName && error.Code == code),
                $"Expected validation code '{code}' for property '{propertyName}'.");
        }
    }

    [TestMethod]
    public void TestTryValidate_PreservesProtocolValidationResponse()
    {
        FirewallRuleSpecification specification = new()
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.Out,
            DestinationInterface = "eth0",
        };

        bool valid = RuleSpecificationValidator.TryValidate(specification, out ModelValidationErrorResponse? response);

        Assert.IsFalse(valid);
        Assert.IsNotNull(response);
        Assert.IsTrue(response.Errors.Any(static error =>
            error.PropertyName == nameof(FirewallRuleSpecification.DestinationInterface)));
    }
}
