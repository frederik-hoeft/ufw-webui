using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Interop.Output;

namespace Ufw.Systemd.Firewall;

internal static class FirewallRuleSet
{
    public static RuleListResponse ToListResponse(UfwStatusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        List<ListedFirewallRule> rules = new(snapshot.Rules.Count);
        foreach (ObservedUfwRule observed in snapshot.Rules)
        {
            rules.Add(UfwRuleMapper.ToListedRule(observed));
        }

        return new RuleListResponse(snapshot.Active, rules);
    }

    public static List<ListedFirewallRule> FindMatches(UfwStatusSnapshot snapshot, string identity) =>
        FindMatches(snapshot, [identity]);

    public static List<ListedFirewallRule> FindMatches(UfwStatusSnapshot snapshot, IReadOnlyList<string> identities)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(identities);
        HashSet<string> identitySet = new(identities, StringComparer.Ordinal);
        List<ListedFirewallRule> matches = [];
        foreach (ObservedUfwRule observed in snapshot.Rules)
        {
            ListedFirewallRule listed = UfwRuleMapper.ToListedRule(observed);
            if (listed.Parsed && listed.RuleId is not null && identitySet.Contains(listed.RuleId))
            {
                matches.Add(listed);
            }
        }

        return matches;
    }
}
