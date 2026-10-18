namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// Resolves raw filter tables into per-root structural views and attaches UFW user-rule enrichment.
/// Traversal inlines chain references. It does not evaluate packet matches or terminal verdicts.
/// </summary>
public static class FilterSystemStateFactory
{
    public static FilterSystemState Create(
        RawFilterTable ipv4,
        RawFilterTable ipv6,
        IReadOnlyList<UfwUserRuleTranslation>? ipv4UserRules = null,
        IReadOnlyList<UfwUserRuleTranslation>? ipv6UserRules = null)
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
            IPv4 = ResolveFamily(ipv4, ipv4UserRules ?? []),
            IPv6 = ResolveFamily(ipv6, ipv6UserRules ?? []),
        };
    }

    private static AddressFamilyFilterState ResolveFamily(RawFilterTable table, IReadOnlyList<UfwUserRuleTranslation> translations)
    {
        Dictionary<FilterRuleLocation, UfwUserRuleReference> links = Associate(table, translations);
        Dictionary<string, RawFilterChain> chains = new(StringComparer.Ordinal);
        foreach (RawFilterChain chain in table.Chains)
        {
            chains[chain.Name] = chain;
        }

        return new AddressFamilyFilterState
        {
            Raw = table,
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
        Walk(chains, links, root, [], new HashSet<string>(StringComparer.Ordinal), rules);
        return new ResolvedFilterChain
        {
            Name = rootName,
            ChainPresent = true,
            Policy = root.Policy,
            Rules = rules,
        };
    }

    private static void Walk(
        Dictionary<string, RawFilterChain> chains,
        Dictionary<FilterRuleLocation, UfwUserRuleReference> links,
        RawFilterChain chain,
        List<string> path,
        HashSet<string> stack,
        List<ResolvedFilterRule> sink)
    {
        if (!stack.Add(chain.Name))
        {
            return;
        }

        path.Add(chain.Name);
        try
        {
            foreach (RawFilterRule rule in chain.Rules)
            {
                string[] chainPath = new string[path.Count];
                path.CopyTo(chainPath);
                links.TryGetValue(rule.Location, out UfwUserRuleReference? userRule);
                sink.Add(new ResolvedFilterRule
                {
                    Location = rule.Location,
                    ProvenanceChain = chain.Name,
                    ChainPath = chainPath,
                    Clauses = rule.Clauses,
                    Target = rule.Target,
                    UserRule = userRule,
                });

                if (rule.Target.IsChainReference
                    && rule.Target.Name is not null
                    && chains.TryGetValue(rule.Target.Name, out RawFilterChain? next))
                {
                    Walk(chains, links, next, path, stack, sink);
                }
            }
        }
        finally
        {
            path.RemoveAt(path.Count - 1);
            stack.Remove(chain.Name);
        }
    }

    private static Dictionary<FilterRuleLocation, UfwUserRuleReference> Associate(
        RawFilterTable table,
        IReadOnlyList<UfwUserRuleTranslation> translations)
    {
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
                UfwUserRuleAttributor.Associate(chain, pending, links);
            }
        }

        return links;
    }
}
