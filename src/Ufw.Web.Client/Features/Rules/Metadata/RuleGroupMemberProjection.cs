using Ufw.Web.Client.Features.Rules;

namespace Ufw.Web.Client.Features.Rules.Metadata;

public sealed record RuleGroupMemberProjection(string RuleId, IReadOnlyList<RuleRowProjection> Occurrences)
{
    public bool IsStale => Occurrences.Count == 0;
}
