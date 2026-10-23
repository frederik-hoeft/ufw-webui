namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// Structural equality used to enrich live rules from UFW's iptables translations.
/// Structured clauses ignore source order. Unknown extensions compare exactly; only documented
/// save-time defaults of known targets/modules are considered equivalent.
/// </summary>
internal static class FilterRuleMatcher
{
    public static bool Matches(RawFilterRule live, TranslatedIptablesRule expected)
    {
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(expected);
        return ClausesMatch(live.Clauses, expected.Clauses) && TargetMatches(live.Target, expected.Target);
    }

    private static bool ClausesMatch(IReadOnlyList<FilterClause> live, IReadOnlyList<FilterClause> expected)
    {
        if (!SameStructuredClauses(live, expected))
        {
            return false;
        }

        List<FilterClause> liveOpaque = OpaqueClauses(live);
        List<FilterClause> expectedOpaque = OpaqueClauses(expected);
        if (liveOpaque.Count != expectedOpaque.Count)
        {
            return false;
        }

        for (int index = 0; index < expectedOpaque.Count; index++)
        {
            if (!OpaqueMatches(expectedOpaque[index], liveOpaque[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TargetMatches(FilterTarget live, FilterTarget expected)
    {
        if (live.Kind != expected.Kind || !string.Equals(live.Name, expected.Name, StringComparison.Ordinal))
        {
            return false;
        }

        if (SameTokens(expected, live))
        {
            return true;
        }

        // iptables-save prints an explicit reject-with option for UFW's implicit default.
        return expected.Name == "REJECT" && string.IsNullOrEmpty(expected.Arguments)
            && IsDefaultRejectArguments(live);

    }

    private static bool SameStructuredClauses(IReadOnlyList<FilterClause> live, IReadOnlyList<FilterClause> expected)
    {
        Dictionary<(FilterClauseKind Kind, bool Negated, string Value), int> liveCounts = StructuredCounts(live);
        Dictionary<(FilterClauseKind Kind, bool Negated, string Value), int> expectedCounts = StructuredCounts(expected);
        if (liveCounts.Count != expectedCounts.Count)
        {
            return false;
        }

        foreach (((FilterClauseKind Kind, bool Negated, string Value) key, int count) in expectedCounts)
        {
            if (!liveCounts.TryGetValue(key, out int liveCount) || liveCount != count)
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<(FilterClauseKind Kind, bool Negated, string Value), int> StructuredCounts(IReadOnlyList<FilterClause> clauses)
    {
        Dictionary<(FilterClauseKind Kind, bool Negated, string Value), int> counts = [];
        foreach (FilterClause clause in clauses)
        {
            if (clause.Kind == FilterClauseKind.Opaque)
            {
                continue;
            }

            (FilterClauseKind, bool, string) key = (clause.Kind, clause.Negated, clause.Value);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return counts;
    }

    private static List<FilterClause> OpaqueClauses(IReadOnlyList<FilterClause> clauses)
    {
        List<FilterClause> opaque = [];
        foreach (FilterClause clause in clauses)
        {
            if (clause.Kind == FilterClauseKind.Opaque)
            {
                opaque.Add(clause);
            }
        }

        return opaque;
    }

    private static bool IsDefaultRejectArguments(FilterTarget target) =>
        FilterExpressionTokens.Of(target).SequenceEqual(["--reject-with", "icmp-port-unreachable"], StringComparer.Ordinal)
        || FilterExpressionTokens.Of(target).SequenceEqual(["--reject-with", "icmp6-port-unreachable"], StringComparer.Ordinal);

    private static bool OpaqueMatches(FilterClause configured, FilterClause observed)
    {
        IReadOnlyList<string> source = FilterExpressionTokens.Of(configured);
        IReadOnlyList<string> live = FilterExpressionTokens.Of(observed);
        if (source.SequenceEqual(live, StringComparer.Ordinal))
        {
            return true;
        }

        // The recent matcher supplies these defaults in some iptables-save versions.
        // No arbitrary live options are ignored: extra predicates must not be mistaken for
        // equivalent configured rules, even if all configured tokens occur in order.
        if (source.Count < 2 || source[0] != "-m" || source[1] != "recent"
            || live.Count < 2 || live[0] != "-m" || live[1] != "recent")
        {
            return false;
        }

        List<string> canonicalLive = [live[0], live[1]];
        for (int index = 2; index < live.Count; index++)
        {
            if (index + 1 < live.Count && live[index] == "--name" && live[index + 1] == "DEFAULT")
            {
                index++;
            }
            else if (index + 1 < live.Count && live[index] == "--mask" && live[index + 1] is "255.255.255.255" or "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff")
            {
                index++;
            }
            else if (live[index] == "--rsource")
            {
                // --rsource is the default address selector for recent.
            }
            else
            {
                canonicalLive.Add(live[index]);
            }
        }

        return source.SequenceEqual(canonicalLive, StringComparer.Ordinal);
    }

    private static bool SameTokens(FilterTarget left, FilterTarget right) =>
        FilterExpressionTokens.Of(left).SequenceEqual(FilterExpressionTokens.Of(right), StringComparer.Ordinal);
}
