using Microsoft.Extensions.Localization;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Web.Client.Services.Localization;

namespace Ufw.Web.Client.Features.Rules.Authoring;

internal sealed class RuleValidationMessageLocalizer(IStringLocalizer<ValidationStrings> validationText)
    : IRuleValidationMessageLocalizer
{
    public string Localize(ModelValidationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        string? key = error.Code switch
        {
            FirewallRuleValidationErrorCodes.ACTION_UNSUPPORTED => "ActionUnsupported",
            FirewallRuleValidationErrorCodes.ADDRESS_FAMILY_UNSUPPORTED => "AddressFamilyUnsupported",
            FirewallRuleValidationErrorCodes.DIRECTION_UNSUPPORTED => "DirectionUnsupported",
            FirewallRuleValidationErrorCodes.PROTOCOL_UNSUPPORTED => "ProtocolUnsupported",
            FirewallRuleValidationErrorCodes.ADDRESS_INVALID => "AddressInvalid",
            FirewallRuleValidationErrorCodes.SCOPED_IPV6_UNSUPPORTED => "ScopedIpv6Unsupported",
            FirewallRuleValidationErrorCodes.IPV4_PREFIX_INVALID => "Ipv4Prefix",
            FirewallRuleValidationErrorCodes.IPV6_PREFIX_INVALID => "Ipv6Prefix",
            FirewallRuleValidationErrorCodes.ADDRESS_FAMILIES_MUST_MATCH => "AddressFamiliesMustMatch",
            FirewallRuleValidationErrorCodes.ADDRESS_FAMILY_MISMATCH => "AddressFamilyMismatch",
            FirewallRuleValidationErrorCodes.PORTS_SYNTAX_INVALID => "PortsSyntax",
            FirewallRuleValidationErrorCodes.PORTS_OUT_OF_RANGE => "PortsRange",
            FirewallRuleValidationErrorCodes.PORT_RANGE_OUT_OF_RANGE => "PortRangesRange",
            FirewallRuleValidationErrorCodes.PORT_RANGE_REVERSED => "PortRangeOrder",
            FirewallRuleValidationErrorCodes.INTERFACE_INVALID => "InterfaceCharacters",
            FirewallRuleValidationErrorCodes.INBOUND_SOURCE_INTERFACE_INVALID => "InboundInterface",
            FirewallRuleValidationErrorCodes.OUTBOUND_DESTINATION_INTERFACE_INVALID => "OutboundInterface",
            FirewallRuleValidationErrorCodes.COMMENT_INVALID => "CommentInvalid",
            FirewallRuleValidationErrorCodes.IPV6_DISABLED => "Ipv6Disabled",
            FirewallRuleValidationErrorCodes.INTERFACE_NOT_FOUND => "InterfaceNotFound",
            _ => null,
        };
        if (key is null)
        {
            return error.ErrorMessage;
        }

        LocalizedString localized = validationText[key];
        return localized.ResourceNotFound ? error.ErrorMessage : localized.Value;
    }
}
