using Ufw.Shared.Domain.Algebra;
using Ufw.Shared.Firewall;

namespace Ufw.Shared.Domain.Projection;

/// <summary>
/// Projects normalized UFW rules into a <see cref="PolicyWorld{TAddress}"/>. The evaluator never sees firewall DTO types.
/// </summary>
/// <remarks>
/// Listed projection follows the address family UFW actually displayed and fails closed when that family contains an opaque row
/// or when IPv6 is disabled. Specification projection is for a hypothetical ordered list and includes a family-neutral rule
/// in the requested family. Interface axes follow UFW's render mapping: inbound ingress is the destination interface,
/// outbound egress is the source interface, and forward uses source as ingress and destination as egress.
/// A referenced interface that is not in the supplied inventory matches no packet instead of becoming "any interface".
/// </remarks>
public static class FirewallPolicyProjector
{
    /// <summary>Gets the protocols the current UFW rule model can name. <c>any</c> expands to this set.</summary>
    public static IReadOnlyList<ProtocolDefinition> SupportedProtocols { get; } = [ProtocolDefinition.Tcp, ProtocolDefinition.Udp];

    /// <summary>Projects the IPv4 rows of an authoritative listing.</summary>
    public static PolicyWorld<uint> ProjectIPv4(
        IReadOnlyList<ListedFirewallRule> rules,
        FirewallConfigurationSnapshot configuration,
        IReadOnlyCollection<string> interfaces)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return ProjectListed(
            rules,
            configuration.IncomingPolicy,
            configuration.OutgoingPolicy,
            configuration.RoutedPolicy,
            interfaces,
            FirewallAddressFamily.IPv4,
            requireIPv6Enabled: false,
            configuration.IPv6Enabled,
            static address => ParseAddress(address, NetworkAddress.ParseIPv4),
            PolicyWorld.CreateIPv4);
    }

    /// <summary>Projects the IPv6 rows of an authoritative listing. Fails when IPv6 is disabled.</summary>
    public static PolicyWorld<UInt128> ProjectIPv6(
        IReadOnlyList<ListedFirewallRule> rules,
        FirewallConfigurationSnapshot configuration,
        IReadOnlyCollection<string> interfaces)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return ProjectListed(
            rules,
            configuration.IncomingPolicy,
            configuration.OutgoingPolicy,
            configuration.RoutedPolicy,
            interfaces,
            FirewallAddressFamily.IPv6,
            requireIPv6Enabled: true,
            configuration.IPv6Enabled,
            static address => ParseAddress(address, NetworkAddress.ParseIPv6),
            PolicyWorld.CreateIPv6);
    }

    /// <summary>Projects a hypothetical IPv4 or family-neutral rule list. IPv6-only rules are skipped.</summary>
    public static PolicyWorld<uint> ProjectIPv4(
        IReadOnlyList<FirewallRuleSpecification> rules,
        FirewallDefaultPolicy incoming,
        FirewallDefaultPolicy outgoing,
        FirewallDefaultPolicy routed,
        IReadOnlyCollection<string> interfaces)
    {
        return ProjectSpecifications(
            rules,
            incoming,
            outgoing,
            routed,
            interfaces,
            FirewallAddressFamily.IPv4,
            static address => ParseAddress(address, NetworkAddress.ParseIPv4),
            PolicyWorld.CreateIPv4);
    }

    /// <summary>Projects a hypothetical IPv6 or family-neutral rule list. IPv4-only rules are skipped.</summary>
    public static PolicyWorld<UInt128> ProjectIPv6(
        IReadOnlyList<FirewallRuleSpecification> rules,
        FirewallDefaultPolicy incoming,
        FirewallDefaultPolicy outgoing,
        FirewallDefaultPolicy routed,
        IReadOnlyCollection<string> interfaces)
    {
        return ProjectSpecifications(
            rules,
            incoming,
            outgoing,
            routed,
            interfaces,
            FirewallAddressFamily.IPv6,
            static address => ParseAddress(address, NetworkAddress.ParseIPv6),
            PolicyWorld.CreateIPv6);
    }

    private static PolicyWorld<TAddress> ProjectListed<TAddress>(
        IReadOnlyList<ListedFirewallRule> rules,
        FirewallDefaultPolicy incoming,
        FirewallDefaultPolicy outgoing,
        FirewallDefaultPolicy routed,
        IReadOnlyCollection<string> interfaces,
        FirewallAddressFamily target,
        bool requireIPv6Enabled,
        bool ipv6Enabled,
        Func<string?, IntervalSet<TAddress>?> parseAddress,
        Func<
            IReadOnlyCollection<ProtocolDefinition>,
            FiniteSet<NetworkInterfaceName>,
            PolicyDecision,
            PolicyDecision,
            PolicyDecision,
            IReadOnlyList<PolicyRuleDefinition<TAddress>>,
            PolicyWorld<TAddress>> create)
        where TAddress : struct, IBinaryInteger<TAddress>, IUnsignedNumber<TAddress>, IMinMaxValue<TAddress>
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (requireIPv6Enabled && !ipv6Enabled)
        {
            throw new FirewallProjectionException("IPv6 is disabled, so there is no active IPv6 user policy to project.");
        }

        FiniteSet<NetworkInterfaceName> interfaceUniverse = NormalizeInterfaces(interfaces);
        List<PolicyRuleDefinition<TAddress>> definitions = [];
        foreach (ListedFirewallRule listed in rules)
        {
            FirewallAddressFamily observed = ObserveFamily(listed);
            if (observed != target)
            {
                continue;
            }

            if (!listed.Parsed || listed.Rule is null)
            {
                throw new FirewallProjectionException(
                    $"Opaque rule '{listed.RawLine}' is in the {target} policy, so the semantic model would be incomplete.");
            }

            FirewallRuleSpecification normalized = NormalizeValidated(listed.Rule);
            string identity = RuleIdentity.Compute(normalized);
            if (listed.RuleId is not null && !string.Equals(listed.RuleId, identity, StringComparison.Ordinal))
            {
                throw new FirewallProjectionException($"Listed rule identity '{listed.RuleId}' does not match its normalized semantics.");
            }

            definitions.Add(ToDefinition(normalized, new RuleId(identity), parseAddress, interfaceUniverse));
        }

        return create(
            SupportedProtocols,
            interfaceUniverse,
            MapDefault(incoming),
            MapDefault(outgoing),
            MapDefault(routed),
            definitions);
    }

    private static PolicyWorld<TAddress> ProjectSpecifications<TAddress>(
        IReadOnlyList<FirewallRuleSpecification> rules,
        FirewallDefaultPolicy incoming,
        FirewallDefaultPolicy outgoing,
        FirewallDefaultPolicy routed,
        IReadOnlyCollection<string> interfaces,
        FirewallAddressFamily target,
        Func<string?, IntervalSet<TAddress>?> parseAddress,
        Func<
            IReadOnlyCollection<ProtocolDefinition>,
            FiniteSet<NetworkInterfaceName>,
            PolicyDecision,
            PolicyDecision,
            PolicyDecision,
            IReadOnlyList<PolicyRuleDefinition<TAddress>>,
            PolicyWorld<TAddress>> create)
        where TAddress : struct, IBinaryInteger<TAddress>, IUnsignedNumber<TAddress>, IMinMaxValue<TAddress>
    {
        ArgumentNullException.ThrowIfNull(rules);
        FiniteSet<NetworkInterfaceName> interfaceUniverse = NormalizeInterfaces(interfaces);
        List<PolicyRuleDefinition<TAddress>> definitions = [];
        foreach (FirewallRuleSpecification specification in rules)
        {
            ArgumentNullException.ThrowIfNull(specification);
            FirewallRuleSpecification normalized = NormalizeValidated(specification);
            if (!IncludeSpecification(normalized, target))
            {
                continue;
            }

            definitions.Add(ToDefinition(normalized, new RuleId(RuleIdentity.Compute(normalized)), parseAddress, interfaceUniverse));
        }

        return create(
            SupportedProtocols,
            interfaceUniverse,
            MapDefault(incoming),
            MapDefault(outgoing),
            MapDefault(routed),
            definitions);
    }

    private static PolicyRuleDefinition<TAddress> ToDefinition<TAddress>(
        FirewallRuleSpecification specification,
        RuleId id,
        Func<string?, IntervalSet<TAddress>?> parseAddress,
        FiniteSet<NetworkInterfaceName> interfaces)
        where TAddress : struct, IBinaryInteger<TAddress>, IUnsignedNumber<TAddress>, IMinMaxValue<TAddress>
    {
        try
        {
            TrafficChain chain = MapChain(specification.Direction);
            (FiniteSet<NetworkInterfaceName>? ingress, FiniteSet<NetworkInterfaceName>? egress) = MapInterfaces(specification, chain, interfaces);
            return new PolicyRuleDefinition<TAddress>
            {
                Id = id,
                Chain = chain,
                Decision = MapAction(specification.Action),
                Source = parseAddress(specification.Source),
                SourcePorts = ParsePorts(specification.SourcePorts),
                Destination = parseAddress(specification.Destination),
                DestinationPorts = ParsePorts(specification.DestinationPorts),
                Protocols = MapProtocols(specification.Protocol),
                Ingress = ingress,
                Egress = egress,
            };
        }
        catch (FormatException exception)
        {
            throw new FirewallProjectionException("A rule address or port could not be projected into the policy model.", exception);
        }
    }

    private static bool IncludeSpecification(FirewallRuleSpecification specification, FirewallAddressFamily target)
    {
        if (specification.AddressFamily == target)
        {
            return true;
        }

        if (specification.AddressFamily != FirewallAddressFamily.Any)
        {
            return false;
        }

        return AddressFits(specification.Source, target) && AddressFits(specification.Destination, target);
    }

    private static bool AddressFits(string? address, FirewallAddressFamily target)
    {
        FirewallAddressFamily family = RuleSpecificationNormalizer.GetAddressFamily(address);
        return family == FirewallAddressFamily.Any || family == target;
    }

    private static FirewallAddressFamily ObserveFamily(ListedFirewallRule rule)
    {
        try
        {
            return ListedFirewallRuleFamily.GetObservedFamily(rule);
        }
        catch (InvalidOperationException exception)
        {
            throw new FirewallProjectionException(
                "An observed rule does not expose an address family, so the policy model would be incomplete.",
                exception);
        }
    }

    private static FirewallRuleSpecification NormalizeValidated(FirewallRuleSpecification specification)
    {
        if (!RuleSpecificationValidator.TryValidate(specification, out _))
        {
            throw new FirewallProjectionException("A rule cannot be projected because its normalized firewall semantics are invalid.");
        }

        return RuleSpecificationNormalizer.Normalize(specification);
    }

    private static FiniteSet<NetworkInterfaceName> NormalizeInterfaces(IReadOnlyCollection<string> interfaces)
    {
        ArgumentNullException.ThrowIfNull(interfaces);
        List<NetworkInterfaceName> names = [];
        foreach (string name in interfaces)
        {
            names.Add(new NetworkInterfaceName(name));
        }

        return FiniteSet<NetworkInterfaceName>.From(names);
    }

    private static IntervalSet<TAddress>? ParseAddress<TAddress>(string? address, Func<string?, Interval<TAddress>> parse)
        where TAddress : struct, IBinaryInteger<TAddress>, IUnsignedNumber<TAddress>, IMinMaxValue<TAddress>
    {
        if (string.IsNullOrWhiteSpace(address) || address.Trim().Equals(RuleSpecificationNormalizer.ANY, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return IntervalSet<TAddress>.Of(parse(address));
    }

    private static IntervalSet<ushort>? ParsePorts(string? ports)
    {
        if (string.IsNullOrWhiteSpace(ports))
        {
            return null;
        }

        return PacketPorts.Parse(ports);
    }

    private static FiniteSet<ProtocolSymbol>? MapProtocols(FirewallProtocol protocol) => protocol switch
    {
        FirewallProtocol.Any => null,
        FirewallProtocol.Tcp => FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp),
        FirewallProtocol.Udp => FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Udp),
        _ => throw new FirewallProjectionException("Unsupported firewall protocol."),
    };

    private static PolicyDecision MapAction(FirewallAction action) => action switch
    {
        FirewallAction.Allow => PolicyDecision.Allow,
        FirewallAction.Deny => PolicyDecision.Deny,
        FirewallAction.Reject => PolicyDecision.Reject,
        FirewallAction.Limit => PolicyDecision.Limit,
        _ => throw new FirewallProjectionException("Unsupported firewall action."),
    };

    private static PolicyDecision MapDefault(FirewallDefaultPolicy policy) => policy switch
    {
        FirewallDefaultPolicy.Allow => PolicyDecision.Allow,
        FirewallDefaultPolicy.Deny => PolicyDecision.Deny,
        FirewallDefaultPolicy.Reject => PolicyDecision.Reject,
        _ => throw new FirewallProjectionException("Unsupported firewall default policy."),
    };

    private static TrafficChain MapChain(FirewallDirection direction) => direction switch
    {
        FirewallDirection.In => TrafficChain.Input,
        FirewallDirection.Out => TrafficChain.Output,
        FirewallDirection.Forward => TrafficChain.Forward,
        _ => throw new FirewallProjectionException("Unsupported firewall direction."),
    };

    private static (FiniteSet<NetworkInterfaceName>? Ingress, FiniteSet<NetworkInterfaceName>? Egress) MapInterfaces(
        FirewallRuleSpecification specification,
        TrafficChain chain,
        FiniteSet<NetworkInterfaceName> interfaces)
    {
        string? ingressName = chain switch
        {
            TrafficChain.Input => specification.DestinationInterface,
            TrafficChain.Forward => specification.SourceInterface,
            _ => null,
        };
        string? egressName = chain switch
        {
            TrafficChain.Output => specification.SourceInterface,
            TrafficChain.Forward => specification.DestinationInterface,
            _ => null,
        };
        if (chain == TrafficChain.Input && !string.IsNullOrEmpty(specification.SourceInterface))
        {
            throw new FirewallProjectionException("Inbound rules cannot name a source interface.");
        }

        if (chain == TrafficChain.Output && !string.IsNullOrEmpty(specification.DestinationInterface))
        {
            throw new FirewallProjectionException("Outbound rules cannot name a destination interface.");
        }

        return (ResolveInterface(ingressName, interfaces), ResolveInterface(egressName, interfaces));
    }

    private static FiniteSet<NetworkInterfaceName>? ResolveInterface(string? name, FiniteSet<NetworkInterfaceName> interfaces)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        NetworkInterfaceName parsed = new(name);
        return interfaces.Contains(parsed)
            ? FiniteSet<NetworkInterfaceName>.Of(parsed)
            : FiniteSet<NetworkInterfaceName>.Empty;
    }
}
