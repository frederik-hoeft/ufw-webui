using Microsoft.Extensions.Localization;
using Moq;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Web.Client.Features.Rules.Authoring;
using Ufw.Web.Client.Services.Localization;
using Ufw.Web.Client.Tests.Support;

namespace Ufw.Web.Client.Tests.Features.Rules.Authoring;

[TestClass]
public sealed class RuleValidationMessageLocalizerTests
{
    [TestMethod]
    [DataRow(FirewallRuleValidationErrorCodes.ACTION_UNSUPPORTED, "ActionUnsupported")]
    [DataRow(FirewallRuleValidationErrorCodes.ADDRESS_FAMILY_UNSUPPORTED, "AddressFamilyUnsupported")]
    [DataRow(FirewallRuleValidationErrorCodes.DIRECTION_UNSUPPORTED, "DirectionUnsupported")]
    [DataRow(FirewallRuleValidationErrorCodes.PROTOCOL_UNSUPPORTED, "ProtocolUnsupported")]
    [DataRow(FirewallRuleValidationErrorCodes.ADDRESS_INVALID, "AddressInvalid")]
    [DataRow(FirewallRuleValidationErrorCodes.SCOPED_IPV6_UNSUPPORTED, "ScopedIpv6Unsupported")]
    [DataRow(FirewallRuleValidationErrorCodes.IPV4_PREFIX_INVALID, "Ipv4Prefix")]
    [DataRow(FirewallRuleValidationErrorCodes.IPV6_PREFIX_INVALID, "Ipv6Prefix")]
    [DataRow(FirewallRuleValidationErrorCodes.ADDRESS_FAMILIES_MUST_MATCH, "AddressFamiliesMustMatch")]
    [DataRow(FirewallRuleValidationErrorCodes.ADDRESS_FAMILY_MISMATCH, "AddressFamilyMismatch")]
    [DataRow(FirewallRuleValidationErrorCodes.PORTS_SYNTAX_INVALID, "PortsSyntax")]
    [DataRow(FirewallRuleValidationErrorCodes.PORTS_OUT_OF_RANGE, "PortsRange")]
    [DataRow(FirewallRuleValidationErrorCodes.PORT_RANGE_OUT_OF_RANGE, "PortRangesRange")]
    [DataRow(FirewallRuleValidationErrorCodes.PORT_RANGE_REVERSED, "PortRangeOrder")]
    [DataRow(FirewallRuleValidationErrorCodes.INTERFACE_INVALID, "InterfaceCharacters")]
    [DataRow(FirewallRuleValidationErrorCodes.INBOUND_SOURCE_INTERFACE_INVALID, "InboundInterface")]
    [DataRow(FirewallRuleValidationErrorCodes.OUTBOUND_DESTINATION_INTERFACE_INVALID, "OutboundInterface")]
    [DataRow(FirewallRuleValidationErrorCodes.COMMENT_INVALID, "CommentInvalid")]
    [DataRow(FirewallRuleValidationErrorCodes.IPV6_DISABLED, "Ipv6Disabled")]
    [DataRow(FirewallRuleValidationErrorCodes.INTERFACE_NOT_FOUND, "InterfaceNotFound")]
    public void Localize_KnownCodeUsesResourceRegardlessOfDiagnosticWording(string code, string resourceKey)
    {
        RuleValidationMessageLocalizer localizer = new(new PassthroughStringLocalizer<ValidationStrings>());

        string result = localizer.Localize(new ModelValidationError("Field", "Diagnostic text deliberately differs from the resource.", code));

        Assert.AreEqual(resourceKey, result);
    }

    [TestMethod]
    public void Localize_MissingOrUnknownCodePreservesDiagnosticWithoutMatchingItsEnglishText()
    {
        RuleValidationMessageLocalizer localizer = new(new PassthroughStringLocalizer<ValidationStrings>());
        const string diagnostic = "Action is not supported.";

        Assert.AreEqual(diagnostic, localizer.Localize(new ModelValidationError("Action", diagnostic)));
        Assert.AreEqual(diagnostic, localizer.Localize(new ModelValidationError("Action", diagnostic, "firewall.rule.future-code")));
    }

    [TestMethod]
    public void Localize_MissingTranslationPreservesOriginalDiagnostic()
    {
        Mock<IStringLocalizer<ValidationStrings>> translations = new();
        translations.Setup(localizer => localizer["ActionUnsupported"])
            .Returns(new LocalizedString("ActionUnsupported", "ActionUnsupported", resourceNotFound: true));
        RuleValidationMessageLocalizer localizer = new(translations.Object);

        string result = localizer.Localize(new ModelValidationError("Action", "Original validator diagnostic.", FirewallRuleValidationErrorCodes.ACTION_UNSUPPORTED));

        Assert.AreEqual("Original validator diagnostic.", result);
    }
}
