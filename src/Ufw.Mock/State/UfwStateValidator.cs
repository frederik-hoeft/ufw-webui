using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Mock.Cli;

namespace Ufw.Mock.State;

internal static class UfwStateValidator
{
    private static readonly HashSet<string> s_firewallPolicies = new(StringComparer.Ordinal)
    {
        "allow", "deny", "reject",
    };

    private static readonly HashSet<string> s_applicationPolicies = new(StringComparer.Ordinal)
    {
        "allow", "deny", "reject", "skip",
    };

    private static readonly HashSet<string> s_loggingLevels = new(StringComparer.Ordinal)
    {
        "off", "low", "medium", "high", "full",
    };

    private static readonly HashSet<string> s_extendedProtocols = new(StringComparer.Ordinal)
    {
        "ah", "esp", "gre", "vrrp", "ipv6", "igmp",
    };

    public static void Validate(UfwMockState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.SchemaVersion != UfwMockState.CURRENT_SCHEMA_VERSION)
        {
            throw Invalid($"Unsupported mock state schema version '{state.SchemaVersion}'.");
        }
        ValidateValue(state.LoggingLevel, s_loggingLevels, "logging level");
        ValidateValue(state.DefaultIncomingPolicy, s_firewallPolicies, "incoming policy");
        ValidateValue(state.DefaultOutgoingPolicy, s_firewallPolicies, "outgoing policy");
        ValidateValue(state.DefaultRoutedPolicy, s_firewallPolicies, "routed policy");
        ValidateValue(state.DefaultApplicationPolicy, s_applicationPolicies, "application policy");

        if (state.Rules is null)
        {
            throw Invalid("Rule collection is missing.");
        }
        if (state.ApplicationProfiles is null)
        {
            throw Invalid("Application-profile collection is missing.");
        }

        ValidateProfiles(state.ApplicationProfiles);
        ValidateRules(state.Rules);
    }

    private static void ValidateProfiles(IReadOnlyList<UfwApplicationProfile> profiles)
    {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (UfwApplicationProfile? profile in profiles)
        {
            if (profile is null
                || string.IsNullOrWhiteSpace(profile.Name)
                || profile.Name.Any(char.IsControl)
                || string.IsNullOrWhiteSpace(profile.Ports))
            {
                throw Invalid("Application profile is malformed.");
            }
            if (!names.Add(profile.Name))
            {
                throw Invalid($"Application profile '{profile.Name}' is duplicated.");
            }
        }
    }

    private static void ValidateRules(IReadOnlyList<UfwMockRule> rules)
    {
        bool ipv6SectionStarted = false;
        foreach (UfwMockRule? rule in rules)
        {
            if (rule?.Specification is null)
            {
                throw Invalid("Firewall rule is malformed.");
            }

            FirewallRuleSpecification specification = rule.Specification;
            if (specification.AddressFamily == FirewallAddressFamily.IPv6)
            {
                ipv6SectionStarted = true;
            }
            else if (specification.AddressFamily == FirewallAddressFamily.IPv4)
            {
                if (ipv6SectionStarted)
                {
                    throw Invalid("IPv4 rules must precede IPv6 rules.");
                }
            }
            else
            {
                throw Invalid("Persisted rules must have a concrete address family.");
            }

            if (!Enum.IsDefined(specification.Action)
                || !Enum.IsDefined(specification.Direction)
                || !Enum.IsDefined(specification.Protocol)
                || !Enum.IsDefined(rule.Logging))
            {
                throw Invalid("Firewall rule contains an unsupported enum value.");
            }
            if (rule.ExtendedProtocol is not null && !s_extendedProtocols.Contains(rule.ExtendedProtocol))
            {
                throw Invalid($"Firewall rule contains unsupported protocol '{rule.ExtendedProtocol}'.");
            }
        }
    }

    private static void ValidateValue(string? value, IReadOnlySet<string> allowed, string description)
    {
        if (value is null || !allowed.Contains(value))
        {
            throw Invalid($"Invalid {description} '{value}'.");
        }
    }

    private static UfwCliException Invalid(string message) => new($"Invalid mock state: {message}");
}
