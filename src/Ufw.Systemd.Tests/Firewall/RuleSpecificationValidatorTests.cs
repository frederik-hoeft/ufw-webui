using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Ipc.Shared.Model.Responses;

namespace Ufw.Systemd.Tests.Firewall;

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
