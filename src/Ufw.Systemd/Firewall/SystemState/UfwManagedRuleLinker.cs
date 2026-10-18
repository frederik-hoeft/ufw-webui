using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.SystemState;

namespace Ufw.Systemd.Firewall.SystemState;

/// <summary>
/// Reconciles file-derived translation groups with independently observed UFW numbered rules.
/// Translation files describe physical expansion, but never establish managed identity on their own.
/// </summary>
internal static class UfwManagedRuleLinker
{
    public static IReadOnlyList<UfwUserRuleTranslation> Link(
        IReadOnlyList<UfwUserRuleTranslation> translations,
        IReadOnlyList<ListedFirewallRule> observedRules,
        FirewallAddressFamily family)
    {
        // An opaque numbered entry has no reliable family or position in the logical user list.
        if (observedRules.Any(static rule => !rule.Parsed || rule.Rule is null))
        {
            return [];
        }

        // The overall tuple-file ordering and numbered-status ordering need not be identical:
        // UFW may display different traffic directions in different groups. Semantic identity
        // includes family and direction. Within one identity, stable UFW order distinguishes
        // otherwise identical occurrences under the no-imperative-edits assumption.
        Dictionary<string, List<ListedFirewallRule>> byIdentity = [];
        foreach (ListedFirewallRule listed in observedRules)
        {
            if (listed.Rule!.AddressFamily != family || listed.RuleId is null)
            {
                continue;
            }

            if (!byIdentity.TryGetValue(listed.RuleId, out List<ListedFirewallRule>? occurrences))
            {
                occurrences = [];
                byIdentity.Add(listed.RuleId, occurrences);
            }

            occurrences.Add(listed);
        }

        Dictionary<string, List<UfwUserRuleTranslation>> configuredByIdentity = [];
        foreach (UfwUserRuleTranslation translation in translations)
        {
            if (translation.Specification is null || translation.RuleId is null)
            {
                continue;
            }

            if (!configuredByIdentity.TryGetValue(translation.RuleId, out List<UfwUserRuleTranslation>? occurrences))
            {
                occurrences = [];
                configuredByIdentity.Add(translation.RuleId, occurrences);
            }

            occurrences.Add(translation);
        }

        Dictionary<int, ListedFirewallRule> links = [];
        foreach ((string identity, List<UfwUserRuleTranslation> configured) in configuredByIdentity)
        {
            if (!byIdentity.TryGetValue(identity, out List<ListedFirewallRule>? observed) || configured.Count != observed.Count)
            {
                // Partial duplicate multiplicities cannot identify a particular managed
                // occurrence. Unrelated identities may still be matched independently.
                continue;
            }

            for (int index = 0; index < configured.Count; index++)
            {
                links.Add(configured[index].Index, observed[index]);
            }
        }

        List<UfwUserRuleTranslation> reconciled = new(links.Count);
        foreach (UfwUserRuleTranslation translation in translations)
        {
            if (!links.TryGetValue(translation.Index, out ListedFirewallRule? managed))
            {
                continue;
            }

            reconciled.Add(new UfwUserRuleTranslation
            {
                Index = translation.Index,
                Specification = managed.Rule,
                RuleId = managed.RuleId,
                IptablesRules = translation.IptablesRules,
            });
        }

        return reconciled;
    }
}
