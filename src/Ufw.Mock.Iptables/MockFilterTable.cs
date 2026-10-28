using System.Text;
using Ufw.Mock.Formatting;
using Ufw.Mock.Services;
using Ufw.Mock.State;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Iptables;

/// <summary>Builds a read-only filter table from the shared mock state and optional iptables-restore fixtures.</summary>
internal sealed class MockFilterTable
{
    private readonly Dictionary<string, Chain> _chains = new(StringComparer.Ordinal);
    private readonly List<Chain> _ordered = [];
    private readonly bool _ufwActive;
    private readonly bool _ipv6Blocked;

    public MockFilterTable(UfwMockState state, FirewallAddressFamily family)
    {
        bool ipv6Available = family != FirewallAddressFamily.IPv6 || new UfwCompatibilityConfiguration().IsIPv6Enabled(state);
        _ufwActive = state.Enabled && ipv6Available;
        _ipv6Blocked = state.Enabled && family == FirewallAddressFamily.IPv6 && !ipv6Available;
        Declare("INPUT", _ipv6Blocked ? "DROP" : _ufwActive ? FormatPolicy(state.DefaultIncomingPolicy) : "ACCEPT");
        Declare("FORWARD", _ipv6Blocked ? "DROP" : _ufwActive ? FormatPolicy(state.DefaultRoutedPolicy) : "ACCEPT");
        Declare("OUTPUT", _ipv6Blocked ? "DROP" : _ufwActive ? FormatPolicy(state.DefaultOutgoingPolicy) : "ACCEPT");
        if (!_ufwActive)
        {
            return;
        }

        string prefix = MockChainNames.Prefix(family);
        // UFW's primary chains are created before its secondary rule/utility chains.
        foreach (string suffix in new[] { "before-logging", "before", "after", "after-logging", "reject", "track" })
        {
            foreach (string direction in new[] { "input", "output", "forward" })
            {
                // No FORWARD tracking chain is installed by UFW 0.36.2.
                if (suffix == "track" && direction == "forward")
                {
                    continue;
                }
                Declare(prefix + suffix + "-" + direction, "-");
            }
        }
        foreach (string name in new[] { "logging-deny", "logging-allow", "skip-to-policy-input", "skip-to-policy-output", "skip-to-policy-forward" })
        {
            Declare(prefix + name, "-");
        }
        if (family == FirewallAddressFamily.IPv4)
        {
            Declare(prefix + "not-local", "-");
        }
        foreach (string name in new[] { "user-input", "user-output", "user-forward", "user-logging-input", "user-logging-output", "user-logging-forward", "user-limit", "user-limit-accept" })
        {
            Declare(prefix + name, "-");
        }
    }

    public void Add(UfwMockState state, FirewallAddressFamily family)
    {
        string prefix = MockChainNames.Prefix(family);
        if (_ipv6Blocked)
        {
            Append("-A INPUT -i lo -j ACCEPT");
            Append("-A OUTPUT -o lo -j ACCEPT");
        }
        if (_ufwActive)
        {
            foreach (string direction in new[] { "input", "output", "forward" })
            {
                string root = direction.ToUpperInvariant();
                foreach (string stage in new[] { "before-logging", "before", "after", "after-logging", "reject" })
                {
                    Append($"-A {root} -j {prefix}{stage}-{direction}");
                }
                if (direction != "forward")
                {
                    Append($"-A {root} -j {prefix}track-{direction}");
                }
            }

            MockUfwDefaultRules.AddBefore(family, prefix, Append);
        }

        LoadFixtures(family, before: true);
        if (_ufwActive)
        {
            // The user chain is reached from the end of the before chain, not
            // directly from INPUT, OUTPUT, or FORWARD.
            foreach (string direction in new[] { "input", "output", "forward" })
            {
                Append($"-A {prefix}before-{direction} -j {prefix}user-{direction}");
            }
            foreach (UfwMockRule rule in state.Rules.Where(rule => rule.Specification.AddressFamily == family))
            {
                foreach (string declaration in MockRuleExpansion.FormatDeclarations(rule))
                {
                    Append(declaration);
                }
            }
            MockUfwDefaultRules.AddAfter(family, prefix, Append);
            Append($"-A {prefix}user-limit -j REJECT");
            Append($"-A {prefix}user-limit-accept -j ACCEPT");
            Append($"-A {prefix}skip-to-policy-input -j {FormatSkipTarget(state.DefaultIncomingPolicy)}");
            Append($"-A {prefix}skip-to-policy-output -j {FormatSkipTarget(state.DefaultOutgoingPolicy)}");
            Append($"-A {prefix}skip-to-policy-forward -j {FormatSkipTarget(state.DefaultRoutedPolicy)}");
            AppendRejectDefault(prefix + "reject-input", state.DefaultIncomingPolicy);
            AppendRejectDefault(prefix + "reject-output", state.DefaultOutgoingPolicy);
            AppendRejectDefault(prefix + "reject-forward", state.DefaultRoutedPolicy);
        }
        LoadFixtures(family, before: false);
    }

    private void AppendRejectDefault(string chain, string policy)
    {
        if (policy == "reject")
        {
            Append("-A " + chain + " -j REJECT");
        }
    }

    private static string FormatSkipTarget(string policy) => policy switch
    {
        "allow" => "ACCEPT",
        "deny" => "DROP",
        "reject" => "REJECT",
        _ => throw new FormatException($"Unknown default UFW policy '{policy}'."),
    };

    public string Format()
    {
        StringBuilder output = new();
        output.AppendLine("# Generated by iptables-save v1.8.10 (Ufw.Mock)");
        output.AppendLine("*filter");
        foreach (Chain chain in _ordered)
        {
            output.Append(':').Append(chain.Name).Append(' ').Append(chain.Policy).AppendLine(" [0:0]");
        }
        foreach (Chain chain in _ordered)
        {
            foreach (string line in chain.Rules)
            {
                output.AppendLine(line);
            }
        }
        output.AppendLine("COMMIT");
        output.AppendLine("# Completed by iptables-save v1.8.10");
        return output.ToString();
    }

    private static string FormatPolicy(string policy) => policy == "allow" ? "ACCEPT" : "DROP";

    private void LoadFixtures(FirewallAddressFamily family, bool before)
    {
        string variable = family == FirewallAddressFamily.IPv4 ? "UFW_MOCK_IPTABLES4_" : "UFW_MOCK_IPTABLES6_";
        variable += before ? "BEFORE_FILES" : "AFTER_FILES";
        string? paths = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(paths))
        {
            return;
        }
        foreach (string path in paths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            ReadFixture(path);
        }
    }

    private void ReadFixture(string path)
    {
        string? table = null;
        bool filterSeen = false;
        foreach (string raw in File.ReadLines(path))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }
            if (line.StartsWith('*'))
            {
                if (table is not null)
                {
                    throw new FormatException($"Nested table in '{path}'.");
                }
                table = line[1..];
                if (table == "filter")
                {
                    filterSeen = true;
                }
            }
            else if (line == "COMMIT")
            {
                if (table is null)
                {
                    throw new FormatException($"Unmatched COMMIT in '{path}'.");
                }
                table = null;
            }
            else if (table == "filter")
            {
                if (line[0] == ':')
                {
                    string[] fields = line[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (fields.Length != 3 || fields[2] != "[0:0]" && !IsCounter(fields[2]))
                    {
                        throw new FormatException($"Invalid chain declaration in '{path}': {line}");
                    }
                    Declare(fields[0], fields[1]);
                }
                else if (line.StartsWith("-A ", StringComparison.Ordinal))
                {
                    Append(line);
                }
                else
                {
                    throw new FormatException($"Unsupported filter declaration in '{path}': {line}");
                }
            }
            else if (table is null)
            {
                throw new FormatException($"Declaration outside a table in '{path}': {line}");
            }
        }
        if (table is not null || !filterSeen)
        {
            throw new FormatException($"Fixture '{path}' needs a complete *filter table.");
        }
    }

    private static bool IsCounter(string field) => field.Length > 2 && field[0] == '[' && field[^1] == ']'
        && field[1..^1].Split(':') is { Length: 2 } parts && parts.All(static part => long.TryParse(part, out long _));

    private void Declare(string name, string policy)
    {
        if (name.Length == 0 || name.Any(static c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')) || policy is not ("-" or "ACCEPT" or "DROP"))
        {
            throw new FormatException($"Invalid filter chain '{name}' or policy '{policy}'.");
        }
        if (_chains.TryGetValue(name, out Chain? existing))
        {
            if (existing.Policy != policy)
            {
                throw new FormatException($"Conflicting filter chain policy for '{name}'.");
            }
            return;
        }
        Chain chain = new(name, policy);
        _chains.Add(name, chain);
        _ordered.Add(chain);
    }

    private void Append(string declaration)
    {
        int start = 3;
        int end = declaration.IndexOf(' ', start);
        if (!declaration.StartsWith("-A ", StringComparison.Ordinal) || end <= start || end == declaration.Length - 1)
        {
            throw new FormatException($"Invalid filter rule: {declaration}");
        }
        string chainName = declaration[start..end];
        if (!_chains.TryGetValue(chainName, out Chain? chain))
        {
            throw new FormatException($"Filter rule references undeclared chain '{chainName}'.");
        }
        chain.Rules.Add(declaration);
    }

    private sealed class Chain(string name, string policy)
    {
        public string Name { get; } = name;
        public string Policy { get; } = policy;
        public List<string> Rules { get; } = [];
    }
}
