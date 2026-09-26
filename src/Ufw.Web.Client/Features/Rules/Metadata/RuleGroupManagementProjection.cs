namespace Ufw.Web.Client.Features.Rules.Metadata;

public sealed record RuleGroupManagementProjection(RuleGroup Group, IReadOnlyList<RuleGroupMemberProjection> Members, bool MemberResolutionAvailable)
{
    public int StoredMemberCount => Group.RuleIds.Count;

    public int LiveOccurrenceCount => MemberResolutionAvailable ? Members.Sum(static member => member.Occurrences.Count) : 0;

    public int StaleMembershipCount => MemberResolutionAvailable ? Members.Count(static member => member.IsStale) : 0;
}
