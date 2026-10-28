using System.Text;
using Ufw.Mock.Formatting;
using Ufw.Mock.Services;
using Ufw.Mock.State;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Filtering;

/// <summary>Maintains an in-memory filter table, its UFW topology, and iptables-save serialization.</summary>
internal sealed class MockFilterTable
{
    private readonly Dictionary<string, Chain> _chains = new(StringComparer.Ordinal);
    private readonly List<Chain> _ordered = [];
    private readonly bool _ufwActive;
    private readonly bool _ipv6Blocked;

    public MockFilterTable(MockFilterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        foreach (MockFilterChain persisted in snapshot.Chains)
        {
            Declare(persisted.Name, persisted.Policy);
            foreach (string rule in persisted.Rules)
            {
                Append(rule);
            }
        }
    }

    public MockFilterSnapshot Snapshot() => new()
    {
        Chains = [.. _ordered.Select(static chain => new MockFilterChain
        {
            Name = chain.Name,
            Policy = chain.Policy,
            Rules = [.. chain.Rules],
        })],
    };

    public MockFilterTable(UfwMockState state, FirewallAddressFamily family, bool? ipv6EnabledAtInstall = null)
    {
        bool ipv6Available = family != FirewallAddressFamily.IPv6 || (ipv6EnabledAtInstall ?? new UfwCompatibilityConfiguration().IsIPv6Enabled(state));
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
            if (family != FirewallAddressFamily.IPv6 || name is not ("user-limit" or "user-limit-accept"))
            {
                Declare(prefix + name, "-");
            }
        }
    }

    public void Add(UfwMockState state, FirewallAddressFamily family, UfwMockInstalledState? installed = null)
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
            ApplyFixtures(installed?.Fixtures(family, before: true) ?? []);

            // The user chain is reached from the end of the before chain, not
            // directly from INPUT, OUTPUT, or FORWARD.
            foreach (string direction in new[] { "input", "output", "forward" })
            {
                Append($"-A {prefix}before-{direction} -j {prefix}user-{direction}");
            }
            // iptables-restore -I rules are installed before the rule-specific logging
            // bodies. Represent that installed ordering in iptables-save's -A syntax.
            foreach (MockIptablesDeclaration declaration in MockUfwAuxiliaryRules.Logging(state, family).Where(static item => item.Insert))
            {
                Append(declaration.ToSaveLine());
            }
            foreach (UfwMockRule rule in state.Rules.Where(rule => rule.Specification.AddressFamily == family))
            {
                foreach (MockIptablesDeclaration declaration in MockRuleExpansion.Expand(rule))
                {
                    Append(declaration.ToSaveLine());
                }
            }
            foreach (MockIptablesDeclaration declaration in MockUfwAuxiliaryRules.Logging(state, family).Where(static item => !item.Insert))
            {
                Append(declaration.ToSaveLine());
            }
            MockUfwDefaultRules.AddAfter(family, prefix, Append);
            foreach (MockIptablesDeclaration declaration in MockUfwAuxiliaryRules.RateLimit(state, family))
            {
                Append(declaration.ToSaveLine());
            }
            Append($"-A {prefix}skip-to-policy-input -j {FormatSkipTarget(state.DefaultIncomingPolicy)}");
            Append($"-A {prefix}skip-to-policy-output -j {FormatSkipTarget(state.DefaultOutgoingPolicy)}");
            Append($"-A {prefix}skip-to-policy-forward -j {FormatSkipTarget(state.DefaultRoutedPolicy)}");
            AppendRejectDefault(prefix + "reject-input", state.DefaultIncomingPolicy);
            AppendRejectDefault(prefix + "reject-output", state.DefaultOutgoingPolicy);
            AppendRejectDefault(prefix + "reject-forward", state.DefaultRoutedPolicy);
            ApplyFixtures(installed?.Fixtures(family, before: false) ?? []);
        }
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

    /// <summary>Stop UFW without deleting unrelated host chains and rules.</summary>
    public void StopUfw(FirewallAddressFamily family)
    {
        string prefix = MockChainNames.Prefix(family);
        foreach (Chain chain in _ordered.ToArray())
        {
            if (chain.Name.StartsWith(prefix, StringComparison.Ordinal))
            {
                _chains.Remove(chain.Name);
                _ordered.Remove(chain);
                continue;
            }
            if (chain.Name is "INPUT" or "OUTPUT" or "FORWARD")
            {
                chain.Policy = "ACCEPT";
            }
            chain.Rules.RemoveAll(rule => rule.Contains("-j " + prefix, StringComparison.Ordinal)
                || rule.Contains("-g " + prefix, StringComparison.Ordinal));
        }
    }

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

    private void ApplyFixtures(IReadOnlyList<MockRestoreSource> sources) =>
        new MockFilterRestoreInterpreter(this).Apply(sources);

    internal void Redeclare(string name, string policy)
    {
        if (_chains.TryGetValue(name, out Chain? existing))
        {
            if (existing.Policy == "-")
            {
                if (policy != "-")
                {
                    throw new FormatException($"User chain '{name}' cannot have a policy.");
                }
                existing.Rules.Clear();
            }
            else if (policy is "ACCEPT" or "DROP")
            {
                existing.Policy = policy;
            }
            else if (policy != "-")
            {
                throw new FormatException($"Invalid policy for builtin chain '{name}'.");
            }
            return;
        }
        Declare(name, policy);
    }

    internal bool HasChain(string name) => _chains.ContainsKey(name);

    internal void FlushChain(string name)
    {
        if (!_chains.TryGetValue(name, out Chain? chain))
        {
            throw new FormatException($"Unknown chain '{name}'.");
        }
        chain.Rules.Clear();
    }

    internal void InsertRule(string declaration, int position)
    {
        (Chain chain, string rule) = ValidateAppend(declaration);
        if (position < 1 || position > chain.Rules.Count + 1)
        {
            throw new FormatException($"Invalid insert position '{position}' for '{chain.Name}'.");
        }
        chain.Rules.Insert(position - 1, rule);
    }

    private void Declare(string name, string policy)
    {
        if (name.Length == 0 || name.Any(static c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')) || policy is not ("-" or "ACCEPT" or "DROP") ||
            policy != "-" && name is not ("INPUT" or "OUTPUT" or "FORWARD"))
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

    internal void Append(string declaration)
    {
        (Chain chain, string rule) = ValidateAppend(declaration);
        chain.Rules.Add(rule);
    }

    private (Chain Chain, string Rule) ValidateAppend(string declaration)
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
        return (chain, declaration);
    }

    private sealed class Chain(string name, string policy)
    {
        public string Name { get; } = name;
        public string Policy { get; set; } = policy;
        public List<string> Rules { get; } = [];
    }
}
