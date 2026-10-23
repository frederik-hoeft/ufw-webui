namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// Reconciles source translations with an observed chain without assuming that configured and
/// installed rule sequences are identical. Only pairs forced by every longest ordered alignment
/// are attributed; indistinguishable partial duplicates remain unattributed.
/// </summary>
internal static class UfwUserRuleAttributor
{
    // Avoid quadratic allocations on unexpectedly large host-supplied rulesets. The inventory
    // remains complete when source enrichment cannot be computed within this bound.
    private const long MaxAlignmentCells = 4_000_000;

    /// <returns>False if the alignment is skipped due to the bounded work limit.</returns>
    public static bool Associate(
        RawFilterChain chain,
        IReadOnlyList<(UfwUserRuleTranslation Translation, TranslatedIptablesRule Rule, int TranslationIndex)> expected,
        Dictionary<FilterRuleLocation, UfwUserRuleReference> links)
    {
        int sourceCount = expected.Count;
        int liveCount = chain.Rules.Count;
        if (sourceCount == 0 || liveCount == 0)
        {
            return true;
        }

        if ((sourceCount + 1L) * (liveCount + 1L) > MaxAlignmentCells)
        {
            return false;
        }

        // The two tables allow us to distinguish a unique correspondence from one of multiple
        // equally valid alignments, including duplicates that disappeared from the live state.
        int width = liveCount + 1;
        int[] prefix = new int[(sourceCount + 1) * width];
        int[] suffix = new int[prefix.Length];
        bool[] equivalent = new bool[sourceCount * liveCount];
        for (int i = 0; i < sourceCount; i++)
        {
            for (int j = 0; j < liveCount; j++)
            {
                equivalent[i * liveCount + j] = FilterRuleMatcher.Matches(chain.Rules[j], expected[i].Rule);
            }
        }

        for (int i = 1; i <= sourceCount; i++)
        {
            for (int j = 1; j <= liveCount; j++)
            {
                int index = i * width + j;
                prefix[index] = equivalent[(i - 1) * liveCount + j - 1]
                    ? prefix[index - width - 1] + 1
                    : Math.Max(prefix[index - width], prefix[index - 1]);
            }
        }

        for (int i = sourceCount - 1; i >= 0; i--)
        {
            for (int j = liveCount - 1; j >= 0; j--)
            {
                int index = i * width + j;
                suffix[index] = equivalent[i * liveCount + j]
                    ? suffix[index + width + 1] + 1
                    : Math.Max(suffix[index + width], suffix[index + 1]);
            }
        }

        int length = prefix[^1];
        if (length == 0)
        {
            return true;
        }

        int[] possibleBySource = new int[sourceCount];
        int[] possibleByLive = new int[liveCount];
        int[] maximumLiveBySource = new int[sourceCount];
        int[] minimumLiveBySource = new int[sourceCount];
        Array.Fill(maximumLiveBySource, -1);
        Array.Fill(minimumLiveBySource, liveCount);
        for (int i = 0; i < sourceCount; i++)
        {
            for (int j = 0; j < liveCount; j++)
            {
                if (equivalent[i * liveCount + j] && prefix[i * width + j] + 1 + suffix[(i + 1) * width + j + 1] == length)
                {
                    possibleBySource[i]++;
                    possibleByLive[j]++;
                    maximumLiveBySource[i] = Math.Max(maximumLiveBySource[i], j);
                    minimumLiveBySource[i] = Math.Min(minimumLiveBySource[i], j);
                }
            }
        }

        int[] largestEarlierLive = new int[sourceCount];
        int[] smallestLaterLive = new int[sourceCount];
        int largest = -1;
        for (int i = 0; i < sourceCount; i++)
        {
            largestEarlierLive[i] = largest;
            largest = Math.Max(largest, maximumLiveBySource[i]);
        }

        int smallest = liveCount;
        for (int i = sourceCount - 1; i >= 0; i--)
        {
            smallestLaterLive[i] = smallest;
            smallest = Math.Min(smallest, minimumLiveBySource[i]);
        }

        // A correspondence is forced only if there is no other optimal alignment
        // that matches either occurrence elsewhere, or crosses this pair in the chain.
        for (int i = 0; i < sourceCount; i++)
        {
            if (possibleBySource[i] != 1)
            {
                continue;
            }

            for (int j = 0; j < liveCount; j++)
            {
                if (!equivalent[i * liveCount + j]
                    || prefix[i * width + j] + 1 + suffix[(i + 1) * width + j + 1] != length
                    || possibleByLive[j] != 1 || largestEarlierLive[i] >= j || smallestLaterLive[i] <= j)
                {
                    continue;
                }

                (UfwUserRuleTranslation translation, _, int translationIndex) = expected[i];
                links.Add(chain.Rules[j].Location, new UfwUserRuleReference
                {
                    UserRuleIndex = translation.Index,
                    TranslationIndex = translationIndex,
                    TranslationCount = translation.IptablesRules.Count,
                    RuleId = translation.RuleId,
                    Specification = translation.Specification,
                });
            }
        }

        return true;
    }
}
