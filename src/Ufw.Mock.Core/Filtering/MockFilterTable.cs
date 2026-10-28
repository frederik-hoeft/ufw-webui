using System.Text;
using Ufw.Mock.Formatting;
using Ufw.Mock.Services;
using Ufw.Mock.State;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Iptables.Grammars;
using Ufw.Shared.Firewall.Iptables.Parsing;

namespace Ufw.Mock.Filtering;

/// <summary>Builds a read-only filter table from the shared mock state and optional iptables-restore fixtures.</summary>
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

    private void ApplyFixtures(IReadOnlyList<MockRestoreSource> sources)
    {
        foreach (MockRestoreSource source in sources)
        {
            ReadFixture(source);
        }
    }

    // iptables-restore --noflush: redeclaring a user-defined chain flushes its
    // existing rules; builtin rules survive while their policy can change.
    private void ReadFixture(MockRestoreSource source)
    {
        string? table = null;
        int lineNumber = 0;
        foreach (string raw in source.Contents.Split('\n'))
        {
            lineNumber++;
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }
            try
            {
                // Non-filter tables are not installed by this mock. Keep their contents
                // opaque, as before, but still enforce the surrounding table boundaries.
                if (table is not null && table != "filter" && !line.StartsWith('*') && line != "COMMIT")
                {
                    continue;
                }
                if (!IptablesRestoreLineGrammar.Instance.TryParse(line, out IptablesRestoreStatement? statement))
                {
                    throw new FormatException($"Unsupported restore declaration: {line}");
                }

                switch (statement.Operation)
                {
                    case IptablesRestoreOperation.Table:
                        if (table is not null)
                        {
                            throw new FormatException("Invalid table declaration.");
                        }
                        table = statement.Name;
                        break;
                    case IptablesRestoreOperation.Commit:
                        if (table is null)
                        {
                            throw new FormatException("Unmatched COMMIT.");
                        }
                        table = null;
                        break;
                    default:
                        if (table is null)
                        {
                            throw new FormatException("Declaration outside a table.");
                        }
                        if (table == "filter")
                        {
                            ApplyFilterStatement(statement);
                        }
                        break;
                }
            }
            catch (FormatException exception)
            {
                throw new FormatException($"{source.Name}:{lineNumber}: {exception.Message}", exception);
            }
        }
        if (table is not null)
        {
            throw new FormatException($"{source.Name}: COMMIT expected.");
        }
    }

    private void ApplyFilterStatement(IptablesRestoreStatement statement)
    {
        switch (statement.Operation)
        {
            case IptablesRestoreOperation.Chain:
                Redeclare(statement.Name!, statement.Policy!);
                break;
            case IptablesRestoreOperation.Append:
                ValidateFixtureRule(statement.Arguments!);
                Append(statement.SourceLine!);
                break;
            case IptablesRestoreOperation.Insert:
                Insert(statement);
                break;
            case IptablesRestoreOperation.Flush:
                if (!_chains.TryGetValue(statement.Name!, out Chain? chain))
                {
                    throw new FormatException($"Unknown chain '{statement.Name}'.");
                }
                chain.Rules.Clear();
                break;
            default:
                throw new FormatException("Unexpected restore statement.");
        }
    }

    private static readonly HashSet<string> s_standardTargets = new(StringComparer.Ordinal)
    {
        "ACCEPT", "DROP", "REJECT", "RETURN", "LOG", "NFLOG", "QUEUE", "NFQUEUE",
        "MARK", "CONNMARK", "NOTRACK", "CT", "AUDIT", "TRACE", "TCPMSS", "ULOG",
    };

    private static readonly HashSet<string> s_valueOptions = new(StringComparer.Ordinal)
    {
        "-p", "--protocol", "-s", "--source", "-d", "--destination",
        "-i", "--in-interface", "-o", "--out-interface", "-m", "--match",
        "--ctstate", "--state", "--icmp-type", "--icmpv6-type", "--dst-type", "--src-type",
        "--dport", "--sport", "--dports", "--sports", "--limit", "--limit-burst",
        "--log-prefix", "--log-level", "--comment", "--reject-with", "--uid-owner", "--gid-owner",
        "--mark", "--set-mark", "--set-xmark", "--seconds", "--hitcount", "--name", "--mask",
        "--tcp-option", "--length", "--ttl", "--hl-eq", "--hl-lt", "--hl-gt",
        "-j", "--jump", "-g", "--goto",
    };

    private static readonly HashSet<string> s_flags = new(StringComparer.Ordinal)
    {
        "!", "--syn", "-f", "--fragment", "--rcheck", "--update", "--set", "--remove",
        "--rsource", "--rdest", "--rttl", "--reap", "--log-tcp-sequence", "--log-tcp-options",
        "--log-ip-options", "--log-uid", "--physdev-is-bridged",
    };

    /// <summary>Validate a supported subset of option semantics after shared grammar tokenization.</summary>
    private void ValidateFixtureRule(IReadOnlyList<IptablesToken> arguments)
    {
        if (arguments.Count == 0)
        {
            throw new FormatException("Missing rule specification.");
        }

        for (int index = 0; index < arguments.Count; index++)
        {
            string option = arguments[index].Text;
            if (arguments[index].Kind == IptablesTokenKind.Negation || s_flags.Contains(option))
            {
                continue;
            }
            int arity = option is "--tcp-flags" or "--match-set" ? 2 : s_valueOptions.Contains(option) ? 1 : 0;
            if (arity == 0 || index + arity >= arguments.Count)
            {
                throw new FormatException($"Unsupported or incomplete filter option '{option}'.");
            }
            for (int operand = index + 1; operand <= index + arity; operand++)
            {
                if (arguments[operand].Kind is not (IptablesTokenKind.BareWord or IptablesTokenKind.QuotedString))
                {
                    throw new FormatException($"Unsupported or incomplete filter option '{option}'.");
                }
            }
            string value = arguments[index + 1].Text;
            if (option is "-j" or "--jump" or "-g" or "--goto")
            {
                if (!s_standardTargets.Contains(value) && !_chains.ContainsKey(value))
                {
                    throw new FormatException($"Unknown jump target '{value}'.");
                }
            }
            else if (option is ("--dport" or "--sport" or "--dports" or "--sports") &&
                !IptablesPredicateNormalizer.TryCanonicalPorts(value, out _))
            {
                throw new FormatException($"Invalid port match '{value}'.");
            }
            index += arity;
        }
    }

    private void Redeclare(string name, string policy)
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

    private void Insert(IptablesRestoreStatement statement)
    {
        IReadOnlyList<IptablesToken> arguments = statement.Arguments!;
        int position = 1;
        int skip = 0;
        if (arguments.Count > 0 && arguments[0].Kind == IptablesTokenKind.BareWord &&
            int.TryParse(arguments[0].Text, out int requestedPosition))
        {
            position = requestedPosition;
            skip = 1;
        }
        ValidateFixtureRule(arguments.Skip(skip).ToArray());
        string ruleLine = "-A " + statement.Name + " " + string.Join(' ', arguments.Skip(skip).Select(static token => token.Format()));
        (Chain chain, string rule) = ValidateAppend(ruleLine);
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

    private void Append(string declaration)
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
