using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Interop.Output;

namespace Ufw.Systemd.Firewall;

internal static class FirewallRuleSet
{
    public static RuleListResponse ToListResponse(UfwStatusSnapshot snapshot, FirewallConfigurationSnapshot configuration)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(configuration);
        List<ListedFirewallRule> rules = new(snapshot.Rules.Count);
        foreach (ObservedUfwRule observed in snapshot.Rules)
        {
            rules.Add(UfwRuleMapper.ToListedRule(observed));
        }

        return new RuleListResponse(snapshot.Active, rules, configuration);
    }

    public static List<ListedFirewallRule> FindMatches(RuleListResponse snapshot, string identity) =>
        FindMatches(snapshot, [identity]);

    public static List<ListedFirewallRule> FindMatches(RuleListResponse snapshot, IReadOnlyList<string> identities)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(identities);
        HashSet<string> identitySet = new(identities, StringComparer.Ordinal);
        return snapshot.Rules
            .Where(rule => rule.Parsed && rule.RuleId is not null && identitySet.Contains(rule.RuleId))
            .ToList();
    }
}
