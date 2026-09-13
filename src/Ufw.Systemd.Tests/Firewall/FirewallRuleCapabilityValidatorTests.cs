using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Systemd.Firewall;

namespace Ufw.Systemd.Tests.Firewall;

[TestClass]
public sealed class FirewallRuleCapabilityValidatorTests
{
    private readonly FirewallRuleCapabilityValidator _validator = new();

    [TestMethod]
    public void Validate_Ipv6RuleWhenIpv6Disabled_ReturnsAddressFamilyValidationError()
    {
        FirewallRuleSpecification rule = new() { AddressFamily = FirewallAddressFamily.IPv6 };

        IResponsePayload? response = _validator.Validate(rule, TestFirewallConfiguration.Disabled);

        ModelValidationErrorResponse validation = Assert.IsInstanceOfType<ModelValidationErrorResponse>(response);
        Assert.HasCount(1, validation.Errors);
        Assert.AreEqual(nameof(FirewallRuleSpecification.AddressFamily), validation.Errors[0].PropertyName);
    }

    [TestMethod]
    public void Validate_Ipv4AndFamilyNeutralRulesWhenIpv6Disabled_AreAccepted()
    {
        Assert.IsNull(_validator.Validate(
            new FirewallRuleSpecification { AddressFamily = FirewallAddressFamily.IPv4 },
            TestFirewallConfiguration.Disabled));
        Assert.IsNull(_validator.Validate(
            new FirewallRuleSpecification { AddressFamily = FirewallAddressFamily.Any },
            TestFirewallConfiguration.Disabled));
    }

    [TestMethod]
    public void Validate_FamilyNeutralRuleWithIpv6AddressWhenIpv6Disabled_ReturnsAddressFamilyValidationError()
    {
        FirewallRuleSpecification rule = new()
        {
            AddressFamily = FirewallAddressFamily.Any,
            Source = "2001:db8::1",
        };

        IResponsePayload? response = _validator.Validate(rule, TestFirewallConfiguration.Disabled);

        ModelValidationErrorResponse validation = Assert.IsInstanceOfType<ModelValidationErrorResponse>(response);
        Assert.HasCount(1, validation.Errors);
        Assert.AreEqual(nameof(FirewallRuleSpecification.AddressFamily), validation.Errors[0].PropertyName);
    }

    [TestMethod]
    public void Validate_Ipv6RuleWhenIpv6Enabled_IsAccepted()
    {
        Assert.IsNull(_validator.Validate(
            new FirewallRuleSpecification { AddressFamily = FirewallAddressFamily.IPv6 },
            TestFirewallConfiguration.Enabled));
    }
}
