using System.Text;
using System.Text.RegularExpressions;
using Ufw.Mock.Formatting;
using Ufw.Mock.Services;
using Ufw.Mock.State;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Filtering;

/// <summary>Builds a read-only filter table from the shared mock state and optional iptables-restore fixtures.</summary>
internal sealed partial class MockFilterTable
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
                if (line.StartsWith('*'))
                {
                    if (table is not null || line.Length == 1)
                    {
                        throw new FormatException("Invalid table declaration.");
                    }
                    table = line[1..];
                }
                else if (line == "COMMIT")
                {
                    if (table is null)
                    {
                        throw new FormatException("Unmatched COMMIT.");
                    }
                    table = null;
                }
                else if (table == "filter")
                {
                    if (line[0] == ':')
                    {
                        string[] fields = line[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (fields.Length != 3 || !IsCounter(fields[2]))
                        {
                            throw new FormatException("Invalid chain declaration.");
                        }
                        Redeclare(fields[0], fields[1]);
                    }
                    else if (line.StartsWith("-A ", StringComparison.Ordinal))
                    {
                        ValidateFixtureRule(line);
                        Append(line);
                    }
                    else if (line.StartsWith("-I ", StringComparison.Ordinal))
                    {
                        ValidateFixtureRule(line);
                        Insert(line);
                    }
                    else if (line.StartsWith("-F ", StringComparison.Ordinal))
                    {
                        string name = line[3..];
                        if (!_chains.TryGetValue(name, out Chain? chain))
                        {
                            throw new FormatException($"Unknown chain '{name}'.");
                        }
                        chain.Rules.Clear();
                    }
                    else
                    {
                        throw new FormatException($"Unsupported filter declaration: {line}");
                    }
                }
                else if (table is null)
                {
                    throw new FormatException("Declaration outside a table.");
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

    [GeneratedRegex("\"[^\"]*\"|'[^']*'|\\S+")]
    private static partial Regex FixtureTokens();

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

    /// <summary>Only a deliberately supported restore vocabulary can become mock kernel state.</summary>
    private void ValidateFixtureRule(string rule)
    {
        // Preserve quoted comment/log prefix arguments while validating their tokens.
        string[] tokens = [.. FixtureTokens().Matches(rule)
            .Select(static match => match.Value.Trim('"', '\''))];
        if (tokens.Length < 3)
        {
            throw new FormatException("Missing rule specification.");
        }
        int start = 2;
        if (tokens[0] == "-I" && uint.TryParse(tokens[2], out _))
        {
            start++;
        }
        if (start >= tokens.Length)
        {
            throw new FormatException("Missing rule specification.");
        }
        for (int i = start; i < tokens.Length; i++)
        {
            string option = tokens[i];
            if (s_flags.Contains(option))
            {
                continue;
            }
            if (option is "--tcp-flags" or "--match-set")
            {
                if (i + 2 >= tokens.Length)
                {
                    throw new FormatException($"Missing operands for '{option}'.");
                }
                i += 2;
                continue;
            }
            if (!s_valueOptions.Contains(option) || i + 1 >= tokens.Length)
            {
                throw new FormatException($"Unsupported or incomplete filter option '{option}'.");
            }
            string argument = tokens[++i];
            if (option is "-j" or "--jump" or "-g" or "--goto")
            {
                if (!s_standardTargets.Contains(argument) && !_chains.ContainsKey(argument))
                {
                    throw new FormatException($"Unknown jump target '{argument}'.");
                }
            }
            else if (option is ("--dport" or "--sport" or "--dports" or "--sports") && !ValidPorts(argument))
            {
                throw new FormatException($"Invalid port match '{argument}'.");
            }
        }
    }

    private static bool ValidPorts(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }
        foreach (string range in value.Split(','))
        {
            string[] endpoints = range.Split(':');
            if (endpoints.Length > 2 || endpoints.Any(static endpoint => endpoint.Length == 0 ||
                !ushort.TryParse(endpoint, out _)))
            {
                return false;
            }
            if (endpoints.Length == 2 && ushort.Parse(endpoints[0]) > ushort.Parse(endpoints[1]))
            {
                return false;
            }
        }
        return true;
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

    private void Insert(string declaration)
    {
        string[] fields = declaration[3..].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 2 || !fields[1].StartsWith('-') && fields[1] != "!" &&
            !int.TryParse(fields[1], out _))
        {
            throw new FormatException($"Invalid insert rule: {declaration}");
        }
        string body = declaration[(3 + fields[0].Length)..].TrimStart();
        int position = 1;
        if (fields.Length == 3 && int.TryParse(fields[1], out int requestedPosition))
        {
            position = requestedPosition;
            body = fields[2];
        }
        (Chain chain, string rule) = ValidateAppend("-A " + fields[0] + " " + body);
        if (position < 1 || position > chain.Rules.Count + 1)
        {
            throw new FormatException($"Invalid insert position '{position}' for '{chain.Name}'.");
        }
        chain.Rules.Insert(position - 1, rule);
    }

    private static bool IsCounter(string field) => field.Length > 2 && field[0] == '[' && field[^1] == ']'
        && field[1..^1].Split(':') is { Length: 2 } parts && parts.All(static part => ulong.TryParse(part, out _));

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
