using Ufw.Mock.State;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Formatting;

/// <summary>UFW 0.36.x logging and IPv4 rate-limit support rules, separate from user-rule expansion.</summary>
internal static class MockUfwAuxiliaryRules
{
    private static readonly string[] s_directions = ["input", "output", "forward"];

    public static IReadOnlyList<MockIptablesDeclaration> Logging(UfwMockState state, FirewallAddressFamily family)
    {
        string prefix = MockChainNames.Prefix(family);
        if (state.LoggingLevel == "off")
        {
            // UFW inserts RETURN at the head of the logging chains so user rules
            // keep their logging branches without emitting any log messages.
            return [.. s_directions.Select(direction =>
                new MockIptablesDeclaration(prefix + "user-logging-" + direction, "-j RETURN", Insert: true))];
        }

        bool rateLimited = state.LoggingLevel is "low" or "medium";
        bool audit = state.LoggingLevel is "medium" or "high" or "full";
        bool auditRateLimited = state.LoggingLevel is "medium" or "high";
        List<MockIptablesDeclaration> declarations = [];

        foreach ((string direction, string policy) in new[]
        {
            ("input", state.DefaultIncomingPolicy),
            ("output", state.DefaultOutgoingPolicy),
            ("forward", state.DefaultRoutedPolicy),
        })
        {
            if (policy != "allow" || state.LoggingLevel != "low")
            {
                string label = policy == "allow" ? "ALLOW" : "BLOCK";
                declarations.Add(new(prefix + "after-logging-" + direction, LogArguments(label, rateLimited)));
            }
        }
        declarations.Add(new(prefix + "logging-deny", LogArguments("BLOCK", rateLimited)));
        declarations.Add(new(prefix + "logging-allow", LogArguments("ALLOW", rateLimited)));

        if (audit)
        {
            foreach (string direction in s_directions)
            {
                declarations.Add(new(prefix + "before-logging-" + direction, LogArguments("AUDIT", auditRateLimited), Insert: true));
            }
        }
        return declarations;
    }

    public static IReadOnlyList<MockIptablesDeclaration> RateLimit(UfwMockState state, FirewallAddressFamily family)
    {
        if (family == FirewallAddressFamily.IPv6)
        {
            return [];
        }
        List<MockIptablesDeclaration> declarations = [];
        if (state.LoggingLevel != "off")
        {
            declarations.Add(new("ufw-user-limit", "-m limit --limit 3/minute -j LOG --log-prefix \"[UFW LIMIT BLOCK] \""));
        }
        declarations.Add(new("ufw-user-limit", "-j REJECT"));
        declarations.Add(new("ufw-user-limit-accept", "-j ACCEPT"));
        return declarations;
    }

    private static string LogArguments(string label, bool rateLimited) =>
        "-j LOG --log-prefix \"[UFW " + label + "] \"" + (rateLimited ? " -m limit --limit 3/min --limit-burst 10" : string.Empty);
}
