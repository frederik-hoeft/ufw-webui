namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// Resolves raw filter tables into per-root structural views and attaches UFW user-rule enrichment.
/// Traversal inlines chain references. It does not evaluate packet matches or terminal verdicts.
/// </summary>
public static class FilterSystemStateFactory
{
    // Flattening a graph can be exponentially larger than its physical rule count. The raw
    // graph remains complete; only the optional per-root structural expansion is bounded.
    private const int MaxTraversalRules = 20_000;
    private const int MaxTraversalDepth = 128;

    public static FilterSystemState Create(
        RawFilterTable ipv4,
        RawFilterTable ipv6,
        IReadOnlyList<UfwUserRuleTranslation>? ipv4UserRules = null,
        IReadOnlyList<UfwUserRuleTranslation>? ipv6UserRules = null,
        FilterEnrichmentReport? ipv4Enrichment = null,
        FilterEnrichmentReport? ipv6Enrichment = null)
    {
        ArgumentNullException.ThrowIfNull(ipv4);
        ArgumentNullException.ThrowIfNull(ipv6);
        if (ipv4.AddressFamily != FirewallAddressFamily.IPv4)
        {
            throw new ArgumentException("The IPv4 table must be an IPv4 filter table.", nameof(ipv4));
        }

        if (ipv6.AddressFamily != FirewallAddressFamily.IPv6)
        {
            throw new ArgumentException("The IPv6 table must be an IPv6 filter table.", nameof(ipv6));
        }

        return new FilterSystemState
        {
            IPv4 = ResolveFamily(ipv4, ipv4UserRules ?? [], ipv4Enrichment),
            IPv6 = ResolveFamily(ipv6, ipv6UserRules ?? [], ipv6Enrichment),
        };
    }

    private static AddressFamilyFilterState ResolveFamily(RawFilterTable table, IReadOnlyList<UfwUserRuleTranslation> translations, FilterEnrichmentReport? sourceReport)
    {
        Dictionary<FilterRuleLocation, UfwUserRuleReference> links = Associate(table, translations, out bool exceededBudget);
        long expectedCount = translations.Sum(static translation => (long)translation.IptablesRules.Count);
        FilterEnrichmentReport enrichment = sourceReport ?? new FilterEnrichmentReport(FilterEnrichmentStatus.Complete);
        if (enrichment.Status is not FilterEnrichmentStatus.Unavailable)
        {
            enrichment = exceededBudget
                ? new FilterEnrichmentReport(FilterEnrichmentStatus.ResourceLimitReached, "The managed-rule alignment exceeded its work limit.")
                : expectedCount != links.Count && enrichment.Status == FilterEnrichmentStatus.Complete
                    ? new FilterEnrichmentReport(FilterEnrichmentStatus.Partial, "Some configured translations could not be matched to active rules.")
                    : enrichment;
        }
        Dictionary<string, RawFilterChain> chains = new(StringComparer.Ordinal);
        foreach (RawFilterChain chain in table.Chains)
        {
            chains[chain.Name] = chain;
        }

        return new AddressFamilyFilterState
        {
            Raw = table,
            Enrichment = enrichment,
            ManagedRuleAttributions = links.Select(static pair => new FilterRuleAttribution(pair.Key, pair.Value)).ToArray(),
            Input = ResolveRoot(table, chains, links, "INPUT"),
            Output = ResolveRoot(table, chains, links, "OUTPUT"),
            Forward = ResolveRoot(table, chains, links, "FORWARD"),
        };
    }

    private static ResolvedFilterChain ResolveRoot(
        RawFilterTable table,
        Dictionary<string, RawFilterChain> chains,
        Dictionary<FilterRuleLocation, UfwUserRuleReference> links,
        string rootName)
    {
        if (!chains.TryGetValue(rootName, out RawFilterChain? root))
        {
            return new ResolvedFilterChain
            {
                Name = rootName,
                ChainPresent = false,
                Policy = FilterChainPolicy.None,
                Rules = [],
            };
        }

        List<ResolvedFilterRule> rules = [];
        FilterTraversalStatus traversalStatus = FilterTraversalStatus.None;
        Walk(chains, links, root, [], [], new HashSet<string>(StringComparer.Ordinal), rules, ref traversalStatus);
        return new ResolvedFilterChain
        {
            Name = rootName,
            ChainPresent = true,
            Policy = root.Policy,
            TraversalStatus = traversalStatus,
            Rules = rules,
        };
    }

    private static bool Walk(
        Dictionary<string, RawFilterChain> chains,
        Dictionary<FilterRuleLocation, UfwUserRuleReference> links,
        RawFilterChain chain,
        List<string> path,
        List<FilterRuleLocation> jumpPath,
        HashSet<string> stack,
        List<ResolvedFilterRule> sink,
        ref FilterTraversalStatus status)
    {
        if (path.Count >= MaxTraversalDepth)
        {
            status |= FilterTraversalStatus.DepthLimitReached;
            return true;
        }

        if (!stack.Add(chain.Name))
        {
            status |= FilterTraversalStatus.CycleDetected;
            return true;
        }

        path.Add(chain.Name);
        try
        {
            foreach (RawFilterRule rule in chain.Rules)
            {
                if (sink.Count >= MaxTraversalRules)
                {
                    status |= FilterTraversalStatus.RuleLimitReached;
                    return false;
                }

                links.TryGetValue(rule.Location, out UfwUserRuleReference? userRule);
                sink.Add(new ResolvedFilterRule
                {
                    Location = rule.Location,
                    ProvenanceChain = chain.Name,
                    ChainPath = [.. path],
                    JumpPath = [.. jumpPath],
                    Clauses = rule.Clauses,
                    Target = rule.Target,
                    UserRule = userRule,
                });

                if (rule.Target.IsChainReference
                    && rule.Target.Name is not null
                    && chains.TryGetValue(rule.Target.Name, out RawFilterChain? next))
                {
                    jumpPath.Add(rule.Location);
                    try
                    {
                        if (!Walk(chains, links, next, path, jumpPath, stack, sink, ref status))
                        {
                            return false;
                        }
                    }
                    finally
                    {
                        jumpPath.RemoveAt(jumpPath.Count - 1);
                    }
                }
            }

            return true;
        }
        finally
        {
            path.RemoveAt(path.Count - 1);
            stack.Remove(chain.Name);
        }
    }

    private static Dictionary<FilterRuleLocation, UfwUserRuleReference> Associate(
        RawFilterTable table,
        IReadOnlyList<UfwUserRuleTranslation> translations,
        out bool exceededBudget)
    {
        exceededBudget = false;
        Dictionary<string, List<(UfwUserRuleTranslation Translation, TranslatedIptablesRule Rule, int TranslationIndex)>> expectedByChain = new(StringComparer.Ordinal);
        foreach (UfwUserRuleTranslation translation in translations)
        {
            ArgumentNullException.ThrowIfNull(translation);
            for (int index = 0; index < translation.IptablesRules.Count; index++)
            {
                TranslatedIptablesRule rule = translation.IptablesRules[index];
                if (!expectedByChain.TryGetValue(rule.ChainName, out List<(UfwUserRuleTranslation, TranslatedIptablesRule, int)>? pending))
                {
                    pending = [];
                    expectedByChain.Add(rule.ChainName, pending);
                }

                pending.Add((translation, rule, index));
            }
        }

        Dictionary<FilterRuleLocation, UfwUserRuleReference> links = [];
        foreach (RawFilterChain chain in table.Chains)
        {
            if (expectedByChain.TryGetValue(chain.Name, out List<(UfwUserRuleTranslation, TranslatedIptablesRule, int)>? pending))
            {
                if (!UfwUserRuleAttributor.Associate(chain, pending, links))
                {
                    exceededBudget = true;
                }
            }
        }

        return links;
    }
}
