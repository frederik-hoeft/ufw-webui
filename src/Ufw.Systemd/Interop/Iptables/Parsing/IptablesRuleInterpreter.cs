using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.SystemState;

namespace Ufw.Systemd.Interop.Iptables.Parsing;

/// <summary>
/// Classifies an iptables rule's tokens into structured UFW predicates and opaque remainder.
/// One classifier is shared by every source that reuses the rule-line grammar.
/// </summary>
internal static class IptablesRuleInterpreter
{
    private static readonly HashSet<string> s_transparentModules = new(StringComparer.Ordinal)
    {
        "tcp",
        "udp",
        "udplite",
        "sctp",
        "dccp",
        "multiport",
    };

    public static bool TryInterpret(
        IReadOnlyList<IptablesToken> tokens,
        FirewallAddressFamily family,
        out IReadOnlyList<FilterClause> clauses,
        out FilterTarget target)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        List<FilterClause> parsed = [];
        List<string> opaque = [];
        string? openModule = null;
        string? protocol = null;
        FilterTargetKind targetKind = FilterTargetKind.None;
        string? targetName = null;
        List<string>? targetArguments = null;
        int index = 0;

        while (index < tokens.Count)
        {
            if (IsClauseStarter(tokens[index]))
            {
                if (!TryConsumeClause(tokens, family, ref index, ref openModule, ref protocol, opaque, parsed, ref targetKind, ref targetName, ref targetArguments))
                {
                    clauses = [];
                    target = UnavailableTarget();
                    return false;
                }

                continue;
            }

            if (tokens[index].Kind == IptablesTokenKind.Negation)
            {
                AppendOpaqueToken(opaque, ref openModule, "!");
                index++;
                continue;
            }

            AppendOpaqueToken(opaque, ref openModule, tokens[index].Format());
            index++;
        }

        FlushOpaque(parsed, opaque, ref openModule);
        openModule = null;
        clauses = parsed;
        target = new FilterTarget
        {
            Kind = targetKind,
            Name = targetName,
            IsChainReference = false,
            Arguments = targetArguments is null || targetArguments.Count == 0 ? null : string.Join(' ', targetArguments),
            ParsedArguments = targetArguments?.ToArray(),
        };
        return targetKind == FilterTargetKind.None || targetName is not null;
    }

    private static bool TryConsumeClause(
        IReadOnlyList<IptablesToken> tokens,
        FirewallAddressFamily family,
        ref int index,
        ref string? openModule,
        ref string? protocol,
        List<string> opaque,
        List<FilterClause> clauses,
        ref FilterTargetKind targetKind,
        ref string? targetName,
        ref List<string>? targetArguments)
    {
        bool negated = false;
        if (tokens[index].Kind == IptablesTokenKind.Negation)
        {
            negated = true;
            index++;
            if (index >= tokens.Count || !IsOption(tokens[index]))
            {
                return false;
            }
        }

        string option = CanonicalOption(tokens[index]);
        int optionIndex = index;
        index++;

        switch (option)
        {
            case "jump":
            case "goto":
                return TryConsumeTarget(option, negated, tokens, ref index, ref openModule, opaque, clauses, ref targetKind, ref targetName, ref targetArguments);
            case "match":
                return TryConsumeModule(negated, tokens, ref index, ref openModule, opaque, clauses);
            case "fragment":
            case "4":
            case "6":
                FlushOpaque(clauses, opaque, ref openModule);
                clauses.Add(OpaqueClause((negated ? "! " : string.Empty) + tokens[optionIndex].Format()));
                return true;
            default:
                if (CanInterpretOption(tokens[optionIndex].Text, option, openModule, protocol)
                    && TryConsumeStructured(option, negated, tokens, family, ref index, out FilterClause? clause))
                {
                    string? activeModule = openModule;
                    FlushOpaque(clauses, opaque, ref openModule);
                    if (option == "protocol")
                    {
                        // A new core protocol selection permits its implicit transport matcher.
                        protocol = clause?.Value;
                        openModule = null;
                    }
                    else
                    {
                        // Address/interface predicates do not end an extension's option scope.
                        // An unknown module may have more options after a core predicate.
                        openModule = activeModule;
                    }

                    if (clause is not null)
                    {
                        clauses.Add(clause);
                    }

                    return true;
                }

                index = optionIndex;
                if (negated)
                {
                    AppendOpaqueToken(opaque, ref openModule, "!");
                }

                AppendOpaqueToken(opaque, ref openModule, tokens[index].Format());
                index++;
                if (index < tokens.Count && tokens[index].Kind is IptablesTokenKind.Negation)
                {
                    AppendOpaqueToken(opaque, ref openModule, "!");
                    index++;
                }

                if (index < tokens.Count && tokens[index].Kind is IptablesTokenKind.BareWord or IptablesTokenKind.QuotedString)
                {
                    AppendOpaqueToken(opaque, ref openModule, tokens[index].Format());
                    index++;
                }

                return true;
        }
    }

    private static bool TryConsumeTarget(
        string option,
        bool negated,
        IReadOnlyList<IptablesToken> tokens,
        ref int index,
        ref string? openModule,
        List<string> opaque,
        List<FilterClause> clauses,
        ref FilterTargetKind targetKind,
        ref string? targetName,
        ref List<string>? targetArguments)
    {
        if (negated || targetName is not null || index >= tokens.Count || tokens[index].Kind is not (IptablesTokenKind.BareWord or IptablesTokenKind.QuotedString))
        {
            return false;
        }

        FlushOpaque(clauses, opaque, ref openModule);
        openModule = null;
        targetKind = option == "goto" ? FilterTargetKind.Goto : FilterTargetKind.Jump;
        targetName = tokens[index].Text;
        index++;
        targetArguments ??= [];
        // Once a target is selected, subsequent options belong to that target. Their
        // names may collide with regular predicate options and must not be promoted.
        while (index < tokens.Count)
        {
            targetArguments.Add(tokens[index].Kind == IptablesTokenKind.Negation ? "!" : tokens[index].Format());
            index++;
        }

        return true;
    }

    private static bool TryConsumeModule(
        bool negated,
        IReadOnlyList<IptablesToken> tokens,
        ref int index,
        ref string? openModule,
        List<string> opaque,
        List<FilterClause> clauses)
    {
        if (index >= tokens.Count || tokens[index].Kind is not (IptablesTokenKind.BareWord or IptablesTokenKind.QuotedString))
        {
            return false;
        }

        FlushOpaque(clauses, opaque, ref openModule);
        string module = tokens[index].Text;
        index++;
        if (!negated && s_transparentModules.Contains(module))
        {
            openModule = module;
            return true;
        }

        if (negated)
        {
            opaque.Add("!");
        }

        opaque.Add("-m");
        opaque.Add(module);
        openModule = module;
        return true;
    }

    private static bool TryConsumeStructured(
        string option,
        bool negated,
        IReadOnlyList<IptablesToken> tokens,
        FirewallAddressFamily family,
        ref int index,
        out FilterClause? clause)
    {
        clause = null;
        int start = index;
        if (option is "protocol" or "source" or "destination" or "in-interface" or "out-interface" or "source-port" or "destination-port" or "ports")
        {
            if (!TryReadValue(tokens, ref index, ref negated, out string value))
            {
                index = start;
                return false;
            }

            switch (option)
            {
                case "protocol":
                    if (!IptablesPredicateNormalizer.TryCanonicalProtocol(value, out string? protocol))
                    {
                        index = start;
                        return false;
                    }

                    if (protocol is null)
                    {
                        if (negated)
                        {
                            clause = Clause(FilterClauseKind.Protocol, negated: true, "all");
                        }

                        return true;
                    }

                    clause = Clause(FilterClauseKind.Protocol, negated, protocol);
                    return true;
                case "source":
                case "destination":
                    if (!IptablesPredicateNormalizer.TryCanonicalAddress(value, family, out string address))
                    {
                        index = start;
                        return false;
                    }

                    if (!negated && address == RuleSpecificationNormalizer.ANY)
                    {
                        return true;
                    }

                    clause = Clause(option == "source" ? FilterClauseKind.SourceAddress : FilterClauseKind.DestinationAddress, negated, address);
                    return true;
                case "in-interface":
                case "out-interface":
                    if (!IptablesPredicateNormalizer.IsInterfaceName(value))
                    {
                        index = start;
                        return false;
                    }

                    clause = Clause(option == "in-interface" ? FilterClauseKind.IngressInterface : FilterClauseKind.EgressInterface, negated, value);
                    return true;
                default:
                    if (!IptablesPredicateNormalizer.TryCanonicalPorts(value, out string ports))
                    {
                        index = start;
                        return false;
                    }

                    FilterClauseKind kind = option switch
                    {
                        "source-port" => FilterClauseKind.SourcePort,
                        "ports" => FilterClauseKind.EitherPort,
                        _ => FilterClauseKind.DestinationPort,
                    };
                    clause = Clause(kind, negated, ports);
                    return true;
            }
        }

        return false;
    }

    private static bool CanInterpretOption(string token, string option, string? module, string? protocol)
    {
        if (option is not ("source-port" or "destination-port" or "ports"))
        {
            return true;
        }

        // A module can introduce arbitrarily named options. Never classify an unknown
        // extension's option solely by its spelling, even when the protocol is TCP/UDP.
        if (module is not null && !s_transparentModules.Contains(module))
        {
            return false;
        }

        bool multiport = token is "--sports" or "--source-ports" or "--dports" or "--destination-ports" or "--ports";
        if (multiport)
        {
            return module == "multiport";
        }

        return module is "tcp" or "udp" or "udplite" or "sctp" or "dccp"
            || (module is null && protocol is "tcp" or "udp" or "udplite" or "sctp" or "dccp");
    }

    private static bool TryReadValue(IReadOnlyList<IptablesToken> tokens, ref int index, ref bool negated, out string value)
    {
        if (index < tokens.Count && tokens[index].Kind == IptablesTokenKind.Negation)
        {
            if (negated)
            {
                value = string.Empty;
                return false;
            }

            negated = true;
            index++;
        }

        if (index >= tokens.Count || tokens[index].Kind is not (IptablesTokenKind.BareWord or IptablesTokenKind.QuotedString))
        {
            value = string.Empty;
            return false;
        }

        value = tokens[index].Text;
        index++;
        return true;
    }

    private static void AppendOpaqueToken(List<string> opaque, ref string? openModule, string text)
    {
        if (opaque.Count == 0 && openModule is not null)
        {
            opaque.Add("-m");
            opaque.Add(openModule);
        }

        opaque.Add(text);
    }

    private static void FlushOpaque(List<FilterClause> clauses, List<string> opaque, ref string? openModule)
    {
        if (opaque.Count == 0)
        {
            return;
        }

        clauses.Add(new FilterClause
        {
            Kind = FilterClauseKind.Opaque,
            Negated = false,
            Value = string.Join(' ', opaque),
            ParsedTokens = opaque.ToArray(),
        });
        opaque.Clear();
        openModule = null;
    }

    private static bool IsClauseStarter(IptablesToken token)
    {
        if (token.Kind == IptablesTokenKind.Negation)
        {
            return true;
        }

        if (!IsOption(token))
        {
            return false;
        }

        string option = CanonicalOption(token);
        return option is "protocol" or "source" or "destination" or "in-interface" or "out-interface"
            or "source-port" or "destination-port" or "ports" or "match" or "jump" or "goto"
            or "fragment" or "4" or "6";
    }

    private static bool IsOption(IptablesToken token) => token.Kind is IptablesTokenKind.ShortOption or IptablesTokenKind.LongOption;

    private static string CanonicalOption(IptablesToken token)
    {
        string name = token.Text.StartsWith("--", StringComparison.Ordinal) ? token.Text[2..] : token.Text[1..];
        return name switch
        {
            "p" or "protocol" => "protocol",
            "s" or "src" or "source" => "source",
            "d" or "dst" or "destination" => "destination",
            "i" or "in-interface" => "in-interface",
            "o" or "out-interface" => "out-interface",
            "m" or "match" => "match",
            "j" or "jump" => "jump",
            "g" or "goto" => "goto",
            "f" or "fragment" => "fragment",
            "4" => "4",
            "6" => "6",
            "sport" or "source-port" or "sports" or "source-ports" => "source-port",
            "dport" or "destination-port" or "dports" or "destination-ports" => "destination-port",
            "ports" => "ports",
            _ => name,
        };
    }

    private static FilterClause Clause(FilterClauseKind kind, bool negated, string value) => new()
    {
        Kind = kind,
        Negated = negated,
        Value = value,
    };

    private static FilterClause OpaqueClause(string value) => Clause(FilterClauseKind.Opaque, negated: false, value);

    private static FilterTarget UnavailableTarget() => new()
    {
        Kind = FilterTargetKind.None,
        IsChainReference = false,
    };
}
