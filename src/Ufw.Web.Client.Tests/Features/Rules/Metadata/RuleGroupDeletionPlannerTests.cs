using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Tests.Features.Rules.Metadata;

[TestClass]
public sealed class RuleGroupDeletionPlannerTests
{
    private static readonly Guid s_groupId = Guid.Parse("0199aabb-ccdd-7eef-8000-000000000001");
    private static readonly Guid s_templateId = Guid.Parse("0199aabb-ccdd-7eef-8000-000000000002");

    [TestMethod]
    public void Create_UsesSnapshotOccurrenceOrderAndPreservesConfirmedMembership()
    {
        RuleGroup group = Group(["two", "one"]) with { TemplateIds = [s_templateId] };
        RuleSnapshot snapshot = Snapshot("one", "other", "one", "two");
        RuleGroupManagementProjection projection = Projection(group,
            Member("two", Row(snapshot, 3)),
            Member("one", Row(snapshot, 2), Row(snapshot, 0)));

        RuleGroupDeletionPlan plan = RuleGroupDeletionPlanner.Create(projection, snapshot);

        Assert.AreEqual(group.Id, plan.GroupId);
        Assert.AreEqual(snapshot.FirewallActive, plan.Baseline.Active);
        Assert.AreSame(snapshot.Rules, plan.Baseline.Rules);
        Assert.AreSame(snapshot.Configuration, plan.Baseline.Configuration);
        CollectionAssert.AreEqual(new[] { 0, 2, 3 }, plan.OccurrenceIds.ToArray());
        CollectionAssert.AreEqual(new[] { "one", "two" }, plan.ExpectedRuleIds.ToArray());
        CollectionAssert.AreEqual(new[] { s_templateId }, plan.ExpectedTemplateIds.ToArray());
    }

    [TestMethod]
    public void Create_RejectsStaleOrUnresolvedMembership()
    {
        RuleGroup group = Group(["missing"]);
        RuleSnapshot snapshot = Snapshot("other");
        RuleGroupManagementProjection stale = Projection(group, Member("missing"));
        RuleGroupManagementProjection unavailable = stale with { MemberResolutionAvailable = false };

        Assert.ThrowsExactly<InvalidOperationException>(() => RuleGroupDeletionPlanner.Create(stale, snapshot));
        Assert.ThrowsExactly<InvalidOperationException>(() => RuleGroupDeletionPlanner.Create(unavailable, snapshot));
        Assert.ThrowsExactly<InvalidOperationException>(() => RuleGroupDeletionPlanner.Create(stale, null));
    }

    [TestMethod]
    public void Create_RejectsIncompleteOrIncorrectConfirmationPreview()
    {
        RuleGroup group = Group(["one"]);
        RuleSnapshot snapshot = Snapshot("one", "one", "other");
        RuleGroupManagementProjection incomplete = Projection(group, Member("one", Row(snapshot, 0)));
        RuleGroupManagementProjection incorrect = Projection(group, Member("one", Row(snapshot, 0), Row(snapshot, 2)));

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => RuleGroupDeletionPlanner.Create(incomplete, snapshot));
        StringAssert.Contains(exception.Message, "preview");
        Assert.ThrowsExactly<InvalidOperationException>(() => RuleGroupDeletionPlanner.Create(incorrect, snapshot));
    }

    [TestMethod]
    public void MatchesConfirmedMembership_IsOrderInsensitiveButRejectsEitherCatalogChange()
    {
        RuleGroup group = Group(["one", "two"]) with { TemplateIds = [s_templateId, Guid.CreateVersion7()] };
        RuleSnapshot snapshot = Snapshot("one", "two");
        RuleGroupDeletionPlan plan = RuleGroupDeletionPlanner.Create(
            Projection(group, Member("one", Row(snapshot, 0)), Member("two", Row(snapshot, 1))), snapshot);

        RuleGroup equivalent = group with { RuleIds = ["two", "one"], TemplateIds = [group.TemplateIds[1], s_templateId] };
        Assert.IsTrue(RuleGroupDeletionPlanner.MatchesConfirmedMembership(plan, equivalent));
        Assert.IsFalse(RuleGroupDeletionPlanner.MatchesConfirmedMembership(plan, group with { RuleIds = ["one"] }));
        Assert.IsFalse(RuleGroupDeletionPlanner.MatchesConfirmedMembership(plan, group with { TemplateIds = [] }));
        Assert.IsFalse(RuleGroupDeletionPlanner.MatchesConfirmedMembership(plan, group with { Id = Guid.CreateVersion7() }));
        Assert.IsFalse(RuleGroupDeletionPlanner.MatchesConfirmedMembership(plan, null));
    }

    [TestMethod]
    public void PlanSingleRuleCleanup_RejectsAmbiguousIdentityOrMissingMetadata()
    {
        RuleGroup group = Group(["one"]);
        RuleSnapshot unique = Snapshot("one");
        RuleSnapshot duplicate = Snapshot("one", "one");

        Assert.IsNull(RuleGroupDeletionPlanner.PlanSingleRuleCleanup(unique.Rules[0], unique));
        Assert.IsNull(RuleGroupDeletionPlanner.PlanSingleRuleCleanup(duplicate.Rules[0], WithMetadata(duplicate, group)));
    }

    [TestMethod]
    public void FindSingleRuleCleanupGroup_RequiresExclusiveOwnershipWithoutTemplates()
    {
        RuleGroup group = Group(["one"]);
        RuleSnapshot snapshot = WithMetadata(Snapshot("one"), group);
        RuleGroupCleanupCandidate candidate = RuleGroupDeletionPlanner.PlanSingleRuleCleanup(snapshot.Rules[0], snapshot)!;

        Assert.AreEqual(group.Id, candidate.GroupId);
        Assert.AreEqual("one", candidate.RuleId);
        Assert.AreSame(group, RuleGroupDeletionPlanner.FindSingleRuleCleanupGroup(candidate, [group]));
        Assert.IsNull(RuleGroupDeletionPlanner.FindSingleRuleCleanupGroup(candidate, [group with { TemplateIds = [s_templateId] }]));
        Assert.IsNull(RuleGroupDeletionPlanner.FindSingleRuleCleanupGroup(candidate, [group with { RuleIds = ["one", "two"] }]));
        Assert.IsNull(RuleGroupDeletionPlanner.FindSingleRuleCleanupGroup(candidate, [group with { RuleIds = ["other"] }]));
    }

    [TestMethod]
    public void CanDeleteEmptyGroup_RequiresNoLiveOrTemplateReferences()
    {
        Assert.IsTrue(RuleGroupDeletionPlanner.CanDeleteEmptyGroup(Group([])));
        Assert.IsFalse(RuleGroupDeletionPlanner.CanDeleteEmptyGroup(Group(["one"])));
        Assert.IsFalse(RuleGroupDeletionPlanner.CanDeleteEmptyGroup(Group([]) with { TemplateIds = [s_templateId] }));
    }

    private static RuleGroup Group(IReadOnlyList<string> ruleIds) => new(s_groupId, "group", null, ruleIds);

    private static RuleSnapshot Snapshot(params string[] ruleIds) => new(true,
        [.. ruleIds.Select(static id => new ListedFirewallRule
        {
            Parsed = true,
            RuleId = id,
            RawLine = id,
            Rule = new FirewallRuleSpecification { AddressFamily = FirewallAddressFamily.IPv4 },
        })],
        TestFirewallConfiguration.Enabled,
        new Dictionary<string, RuleMetadata>(StringComparer.Ordinal));

    private static RuleSnapshot WithMetadata(RuleSnapshot snapshot, RuleGroup group) => new(snapshot.FirewallActive,
        snapshot.Rules,
        snapshot.Configuration,
        new Dictionary<string, RuleMetadata>(StringComparer.Ordinal)
        {
            ["one"] = new RuleMetadata(Guid.CreateVersion7(), null, [], new RuleGroupMembership(group.Id, group.Name, group.Comment)),
        });

    private static RuleRowProjection Row(RuleSnapshot snapshot, int occurrenceId) =>
        new(snapshot.Rules[occurrenceId], FirewallAddressFamily.IPv4, occurrenceId, occurrenceId + 1, FamilyCount: snapshot.Rules.Count,
            CanOrder: true, CanMutate: true, PositionChange: null);

    private static RuleGroupMemberProjection Member(string ruleId, params RuleRowProjection[] occurrences) => new(ruleId, occurrences);

    private static RuleGroupManagementProjection Projection(RuleGroup group, params RuleGroupMemberProjection[] members) => new(group, members, MemberResolutionAvailable: true);
}
