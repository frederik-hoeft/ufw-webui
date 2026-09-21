using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Authoring;
using Ufw.Web.Client.Services.Localization;
using Ufw.Web.Client.Tests.Support;

namespace Ufw.Web.Client.Tests.Rules.Authoring;

[TestClass]
public sealed class RuleEditorValidationServiceTests
{
    private readonly RuleEditorValidationService _service = new(
        new RuleValidationMessageLocalizer(new PassthroughStringLocalizer<ValidationStrings>()),
        new PassthroughStringLocalizer<ValidationStrings>());

    [TestMethod]
    public void Validate_LocalizesDomainValidationForRequestedProperty()
    {
        FirewallRuleSpecification rule = ValidRule();
        rule.Action = (FirewallAction)int.MaxValue;

        IReadOnlyList<string> actionErrors = _service.Validate(rule, nameof(FirewallRuleSpecification.Action), ipv6Enabled: true);
        IReadOnlyList<string> protocolErrors = _service.Validate(rule, nameof(FirewallRuleSpecification.Protocol), ipv6Enabled: true);

        CollectionAssert.AreEqual(new[] { "ActionUnsupported" }, actionErrors.ToArray());
        Assert.IsEmpty(protocolErrors);
    }

    [TestMethod]
    public void Validate_AddsCapabilityErrorsWhenIpv6IsDisabled()
    {
        FirewallRuleSpecification rule = ValidRule();
        rule.AddressFamily = FirewallAddressFamily.IPv6;
        rule.Source = "2001:db8::1";
        rule.Destination = "2001:db8::2";

        CollectionAssert.Contains(_service.Validate(rule, nameof(FirewallRuleSpecification.AddressFamily), ipv6Enabled: false).ToArray(), "Ipv6Disabled");
        CollectionAssert.Contains(_service.Validate(rule, nameof(FirewallRuleSpecification.Source), ipv6Enabled: false).ToArray(), "Ipv6Disabled");
        CollectionAssert.Contains(_service.Validate(rule, $"Rule.{nameof(FirewallRuleSpecification.Destination)}", ipv6Enabled: false).ToArray(), "Ipv6Disabled");
    }

    [TestMethod]
    public void Validate_DoesNotAddCapabilityErrorsWhenIpv6IsEnabled()
    {
        FirewallRuleSpecification rule = ValidRule();
        rule.AddressFamily = FirewallAddressFamily.IPv6;
        rule.Source = "2001:db8::1";
        rule.Destination = "2001:db8::2";

        Assert.IsFalse(_service.Validate(rule, nameof(FirewallRuleSpecification.AddressFamily), ipv6Enabled: true).Contains("Ipv6Disabled", StringComparer.Ordinal));
    }

    private static FirewallRuleSpecification ValidRule() => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.IPv4,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        Source = "192.0.2.1",
        Destination = "198.51.100.1",
    };
}
