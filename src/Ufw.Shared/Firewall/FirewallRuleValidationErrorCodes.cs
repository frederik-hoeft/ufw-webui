namespace Ufw.Shared.Firewall;

/// <summary>
/// Stable machine-readable identities for firewall rule validation failures.
/// </summary>
/// <remarks>
/// These values are serialized across IPC boundaries. Consumers should use them for validation identity rather than matching diagnostic text.
/// </remarks>
public static class FirewallRuleValidationErrorCodes
{
    public const string ACTION_UNSUPPORTED = "firewall.rule.action.unsupported";
    public const string ADDRESS_FAMILY_UNSUPPORTED = "firewall.rule.address-family.unsupported";
    public const string DIRECTION_UNSUPPORTED = "firewall.rule.direction.unsupported";
    public const string PROTOCOL_UNSUPPORTED = "firewall.rule.protocol.unsupported";
    public const string ADDRESS_INVALID = "firewall.rule.address.invalid";
    public const string SCOPED_IPV6_UNSUPPORTED = "firewall.rule.address.scoped-ipv6-unsupported";
    public const string IPV4_PREFIX_INVALID = "firewall.rule.address.ipv4-prefix-invalid";
    public const string IPV6_PREFIX_INVALID = "firewall.rule.address.ipv6-prefix-invalid";
    public const string ADDRESS_FAMILIES_MUST_MATCH = "firewall.rule.address-family.mixed";
    public const string ADDRESS_FAMILY_MISMATCH = "firewall.rule.address-family.mismatch";
    public const string PORTS_SYNTAX_INVALID = "firewall.rule.ports.syntax-invalid";
    public const string PORTS_OUT_OF_RANGE = "firewall.rule.ports.out-of-range";
    public const string PORT_RANGE_OUT_OF_RANGE = "firewall.rule.port-range.out-of-range";
    public const string PORT_RANGE_REVERSED = "firewall.rule.port-range.reversed";
    public const string INTERFACE_INVALID = "firewall.rule.interface.invalid";
    public const string INBOUND_SOURCE_INTERFACE_INVALID = "firewall.rule.interface.inbound-source-invalid";
    public const string OUTBOUND_DESTINATION_INTERFACE_INVALID = "firewall.rule.interface.outbound-destination-invalid";
    public const string COMMENT_INVALID = "firewall.rule.comment.invalid";
    public const string IPV6_DISABLED = "firewall.rule.ipv6.disabled";
    public const string INTERFACE_NOT_FOUND = "firewall.rule.interface.not-found";
}
