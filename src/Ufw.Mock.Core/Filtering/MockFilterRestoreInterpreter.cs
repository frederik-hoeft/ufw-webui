using Ufw.Mock.State;
using Ufw.Shared.Firewall.Iptables.Grammars;
using Ufw.Shared.Firewall.Iptables.Parsing;

namespace Ufw.Mock.Filtering;

/// <summary>
/// Interprets an explicitly supported subset of iptables-restore --noflush fixtures.
/// Parses statements using shared grammars and applies their effects to a mock filter table.
/// Unsupported kernel extensions fail instead of creating an impossible installed snapshot.
/// </summary>
internal sealed class MockFilterRestoreInterpreter(MockFilterTable table)
{
    private readonly MockFilterTable _table = table;

    public void Apply(IReadOnlyList<MockRestoreSource> sources)
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
                _table.Redeclare(statement.Name!, statement.Policy!);
                break;
            case IptablesRestoreOperation.Append:
                ValidateFixtureRule(statement.Arguments!);
                _table.Append(statement.SourceLine!);
                break;
            case IptablesRestoreOperation.Insert:
                Insert(statement);
                break;
            case IptablesRestoreOperation.Flush:
                _table.FlushChain(statement.Name!);
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
                if (!s_standardTargets.Contains(value) && !_table.HasChain(value))
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
        _table.InsertRule(ruleLine, position);
    }
}
