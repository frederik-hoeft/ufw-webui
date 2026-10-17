namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// Structural equality used to enrich live rules from UFW's iptables translations.
/// Structured clauses ignore source order. Opaque text and target arguments allow the live
/// rule to carry extra iptables-save default tokens.
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
            if (!TokensCover(expectedOpaque[index].Value, liveOpaque[index].Value))
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

        return TokensCover(expected.Arguments, live.Arguments);
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

    /// <summary>
    /// True when every token of <paramref name="expected"/> occurs, in order, in <paramref name="live"/>.
    /// An empty expected argument list matches any live arguments.
    /// </summary>
    private static bool TokensCover(string? expected, string? live)
    {
        List<string> expectedTokens = Tokenize(expected);
        if (expectedTokens.Count == 0)
        {
            return true;
        }

        List<string> liveTokens = Tokenize(live);
        int liveIndex = 0;
        foreach (string token in expectedTokens)
        {
            bool found = false;
            while (liveIndex < liveTokens.Count)
            {
                string candidate = liveTokens[liveIndex];
                liveIndex++;
                if (string.Equals(candidate, token, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    private static List<string> Tokenize(string? text)
    {
        List<string> tokens = [];
        if (string.IsNullOrWhiteSpace(text))
        {
            return tokens;
        }

        int index = 0;
        while (index < text.Length)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            if (index >= text.Length)
            {
                break;
            }

            if (text[index] is '"' or '\'')
            {
                char quote = text[index];
                index++;
                int start = index;
                while (index < text.Length && text[index] != quote)
                {
                    index++;
                }

                tokens.Add(text[start..index]);
                if (index < text.Length)
                {
                    index++;
                }

                continue;
            }

            int wordStart = index;
            while (index < text.Length && !char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            tokens.Add(text[wordStart..index]);
        }

        return tokens;
    }
}
