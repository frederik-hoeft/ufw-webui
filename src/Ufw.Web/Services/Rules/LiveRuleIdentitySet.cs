using System.Collections;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Services.Rules;

/// <summary>
/// Represents the live semantic rule identities exposed by one authoritative firewall snapshot.
/// </summary>
internal sealed class LiveRuleIdentitySet : IReadOnlyCollection<string>
{
    private readonly string[] _ordered;
    private readonly HashSet<string> _identities;

    private LiveRuleIdentitySet(string[] ordered)
    {
        _ordered = ordered;
        _identities = new HashSet<string>(ordered, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public int Count => _ordered.Length;

    /// <summary>
    /// Builds the live semantic identity set from an authoritative daemon snapshot.
    /// </summary>
    public static LiveRuleIdentitySet FromSnapshot(RuleListResponse snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string[] ordered = [.. snapshot.Rules
            .Select(static rule => rule.RuleId)
            .Where(static ruleId => !string.IsNullOrWhiteSpace(ruleId))
            .Select(static ruleId => ruleId!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
        return new LiveRuleIdentitySet(ordered);
    }

    /// <summary>
    /// Determines whether the semantic rule identity is live in the snapshot.
    /// </summary>
    public bool Contains(string ruleId) => _identities.Contains(ruleId);

    /// <inheritdoc />
    public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)_ordered).GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
