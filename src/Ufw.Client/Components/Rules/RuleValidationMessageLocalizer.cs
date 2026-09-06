using Microsoft.Extensions.Localization;
using Ufw.Client.Localization;
using Ufw.Shared.Ipc.Model.Responses;

namespace Ufw.Client.Components.Rules;

internal sealed class RuleValidationMessageLocalizer(IStringLocalizer<ValidationStrings> validationText)
    : IRuleValidationMessageLocalizer
{
    private static readonly IReadOnlyDictionary<string, string> s_resourceKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Action is not supported."] = "ActionUnsupported",
        ["Address family is not supported."] = "AddressFamilyUnsupported",
        ["Direction is not supported."] = "DirectionUnsupported",
        ["Protocol is not supported."] = "ProtocolUnsupported",
        ["Address must be IPv4, IPv6, CIDR, or 'any'."] = "AddressInvalid",
        ["Scoped IPv6 addresses are not supported; select the interface explicitly."] = "ScopedIpv6Unsupported",
        ["IPv4 prefix length must be between 0 and 32."] = "Ipv4Prefix",
        ["IPv6 prefix length must be between 0 and 128."] = "Ipv6Prefix",
        ["Source and destination addresses must use the same address family."] = "AddressFamiliesMustMatch",
        ["Address family does not match the rule addresses."] = "AddressFamilyMismatch",
        ["Ports must be a comma-separated list of ports or port ranges."] = "PortsSyntax",
        ["Ports must be between 1 and 65535."] = "PortsRange",
        ["Port ranges must use values between 1 and 65535."] = "PortRangesRange",
        ["Port range start must be less than or equal to the end."] = "PortRangeOrder",
        ["Interface name contains unsupported characters."] = "InterfaceCharacters",
        ["Inbound rules cannot specify a source interface; use DestinationInterface for the ingress interface."] = "InboundInterface",
        ["Outbound rules cannot specify a destination interface; use SourceInterface for the egress interface."] = "OutboundInterface",
        ["Comment must be 1-200 characters of a restricted safe alphabet."] = "CommentInvalid",
    };

    public string Localize(ModelValidationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return s_resourceKeys.TryGetValue(error.ErrorMessage, out string? key)
            ? validationText[key]
            : error.ErrorMessage;
    }
}
