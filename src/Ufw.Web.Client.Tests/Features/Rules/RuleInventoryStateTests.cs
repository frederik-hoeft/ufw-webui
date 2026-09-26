using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Model.V1.RuleGroups;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Model.V1.RuleTags;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.Tests.Features.Rules;

[TestClass]
public sealed class RuleInventoryStateTests
{
    [TestMethod]
    public void CompleteRefresh_LoadsMetadataBySemanticIdentity()
    {
        Guid metadataId = Guid.CreateVersion7();
        Guid tagId = Guid.CreateVersion7();
        Guid groupId = Guid.CreateVersion7();
        RuleInventoryResponse response = Inventory(
            new RuleListResponse(true, [Rule("shared"), Rule("shared")], TestFirewallConfiguration.Enabled),
            [new RuleMetadataItem
            {
                Id = metadataId,
                RuleId = "shared",
                Notes = "managed",
                Tags = [new RuleTagItem { Id = tagId, Name = "prod", Color = "#336699" }],
                Group = new RuleGroupSummary(groupId, "operations", "trusted access"),
            }]);

        RuleInventoryState state = Loaded(response);

        Assert.IsNotNull(state.Snapshot);
        Assert.HasCount(1, state.Snapshot.Metadata);
        Assert.AreEqual(metadataId, state.Snapshot.Metadata["shared"].Id);
        Assert.AreEqual("managed", state.Snapshot.Metadata["shared"].Notes);
        Assert.HasCount(1, state.Snapshot.Metadata["shared"].Tags);
        Assert.AreEqual(tagId, state.Snapshot.Metadata["shared"].Tags[0].Id);
        Assert.AreEqual("prod", state.Snapshot.Metadata["shared"].Tags[0].Name);
        Assert.AreEqual("#336699", state.Snapshot.Metadata["shared"].Tags[0].Color);
        Assert.AreEqual(groupId, state.Snapshot.Metadata["shared"].Group?.Id);
        Assert.AreEqual("operations", state.Snapshot.Metadata["shared"].Group?.Name);
        Assert.AreEqual("trusted access", state.Snapshot.Metadata["shared"].Group?.Comment);
        Assert.AreEqual(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero), state.Snapshot.CapturedAt);
    }

    [TestMethod]
    public void AfterMutationSnapshot_PreservesOnlyMetadataForStillLiveSemanticIdentities()
    {
        RuleInventoryResponse response = Inventory(
            new RuleListResponse(true, [Rule("keep"), Rule("remove")], TestFirewallConfiguration.Enabled),
            [
                new RuleMetadataItem { Id = Guid.CreateVersion7(), RuleId = "keep", Tags = [] },
                new RuleMetadataItem { Id = Guid.CreateVersion7(), RuleId = "remove", Tags = [] },
            ]);
        RuleInventoryState state = Loaded(response);
        RuleListResponse finalSnapshot = new(true, [Rule("keep"), Rule("new")], TestFirewallConfiguration.Enabled);
        RuleInsertionResponse report = new(RuleInsertionOutcome.Completed, finalSnapshot, Rule("new"), Diagnostic: null);

        RuleInventoryState updated = state.MoveNext(new RuleInventoryTransition.InsertionCompleted(report));

        Assert.IsNotNull(updated.Snapshot);
        Assert.HasCount(1, updated.Snapshot.Metadata);
        Assert.IsTrue(updated.Snapshot.Metadata.ContainsKey("keep"));
        Assert.IsFalse(updated.Snapshot.Metadata.ContainsKey("remove"));
        Assert.IsFalse(updated.Snapshot.Metadata.ContainsKey("new"));
    }

    [TestMethod]
    public void AfterMetadataMutation_UpdatesAndClearsMetadataWithoutReplacingFirewallState()
    {
        Guid metadataId = Guid.CreateVersion7();
        Guid tagId = Guid.CreateVersion7();
        RuleInventoryState state = Loaded(Inventory(new RuleListResponse(true, [Rule("rule")], TestFirewallConfiguration.Enabled)));
        RuleMetadataMutationResponse saved = new()
        {
            Metadata = new RuleMetadataItem
            {
                Id = metadataId,
                RuleId = "rule",
                Notes = "  owned by platform  ",
                Tags = [new RuleTagItem { Id = tagId, Name = " prod ", Color = "#aabbcc" }],
            },
        };

        RuleInventoryState updated = state.MoveNext(new RuleInventoryTransition.MetadataMutationCompleted("rule", saved));

        Assert.IsTrue(updated.IsCurrent);
        Assert.AreSame(state.Snapshot!.Rules, updated.Snapshot!.Rules);
        Assert.AreEqual("owned by platform", updated.Snapshot.Metadata["rule"].Notes);
        Assert.AreEqual("prod", updated.Snapshot.Metadata["rule"].Tags.Single().Name);
        Assert.AreEqual("#AABBCC", updated.Snapshot.Metadata["rule"].Tags.Single().Color);

        RuleInventoryState cleared = updated.MoveNext(new RuleInventoryTransition.MetadataMutationCompleted("rule", new RuleMetadataMutationResponse()));
        Assert.IsEmpty(cleared.Snapshot!.Metadata);
        Assert.AreSame(updated.Snapshot.Rules, cleared.Snapshot.Rules);
    }

    [TestMethod]
    public void AfterMetadataMutation_RejectsMismatchedOrUnknownRuleIdentity()
    {
        RuleInventoryState state = Loaded(Inventory(new RuleListResponse(true, [Rule("rule")], TestFirewallConfiguration.Enabled)));
        RuleMetadataMutationResponse mismatched = new()
        {
            Metadata = new RuleMetadataItem
            {
                Id = Guid.CreateVersion7(),
                RuleId = "other",
                Tags = [],
            },
        };

        Assert.ThrowsExactly<ApiProtocolException>(() => state.MoveNext(new RuleInventoryTransition.MetadataMutationCompleted("rule", mismatched)));
        Assert.ThrowsExactly<InvalidOperationException>(() => state.MoveNext(new RuleInventoryTransition.MetadataMutationCompleted("missing", new RuleMetadataMutationResponse())));
    }

    [TestMethod]
    public void ReconcileTagCatalog_RefreshesTagPresentationByStableIdentity()
    {
        Guid tagId = Guid.CreateVersion7();
        RuleInventoryState state = Loaded(Inventory(
            new RuleListResponse(true, [Rule("rule")], TestFirewallConfiguration.Enabled),
            [new RuleMetadataItem
            {
                Id = Guid.CreateVersion7(),
                RuleId = "rule",
                Tags = [new RuleTagItem { Id = tagId, Name = "old", Color = "#112233" }],
            }]));

        RuleInventoryState updated = state.MoveNext(new RuleInventoryTransition.TagCatalogReconciled([new Ufw.Web.Client.Features.Rules.Metadata.RuleTag(tagId, "new", "#AABBCC")]));

        Assert.AreEqual("new", updated.Snapshot!.Metadata["rule"].Tags.Single().Name);
        Assert.AreEqual("#AABBCC", updated.Snapshot.Metadata["rule"].Tags.Single().Color);
    }

    [TestMethod]
    public void ReconcileGroupCatalog_RefreshesGroupPresentationByStableIdentity()
    {
        Guid groupId = Guid.CreateVersion7();
        RuleInventoryState state = Loaded(Inventory(
            new RuleListResponse(true, [Rule("rule")], TestFirewallConfiguration.Enabled),
            [new RuleMetadataItem
            {
                Id = Guid.CreateVersion7(),
                RuleId = "rule",
                Tags = [],
                Group = new RuleGroupSummary(groupId, "old", "old comment"),
            }]));

        RuleInventoryState updated = state.MoveNext(new RuleInventoryTransition.GroupCatalogReconciled([new RuleGroup(groupId, "new", "new comment", ["rule"])]));

        Assert.AreEqual("new", updated.Snapshot!.Metadata["rule"].Group!.Name);
        Assert.AreEqual("new comment", updated.Snapshot.Metadata["rule"].Group!.Comment);
    }

    [TestMethod]
    public void AfterInsertion_WithAuthoritativeFinalSnapshotReplacesLocalAuthority()
    {
        RuleInventoryState state = Loaded(Inventory(new RuleListResponse(true, [Rule("old")], TestFirewallConfiguration.Enabled)));
        RuleListResponse finalSnapshot = new(false, [Rule("old"), Rule("inserted")], TestFirewallConfiguration.Disabled);
        RuleInsertionResponse report = new(RuleInsertionOutcome.PreconditionFailed, finalSnapshot, InsertedRule: null, Diagnostic: "rejected");

        DateTimeOffset capturedAt = new(2026, 9, 25, 14, 15, 0, TimeSpan.Zero);
        RuleInventoryState updated = state.MoveNext(new RuleInventoryTransition.InsertionCompleted(report, capturedAt));

        Assert.IsTrue(updated.IsCurrent);
        Assert.IsNotNull(updated.Snapshot);
        Assert.IsFalse(updated.Snapshot.FirewallActive);
        Assert.HasCount(2, updated.Snapshot.Rules);
        Assert.AreEqual("inserted", updated.Snapshot.Rules[1].RuleId);
        Assert.IsFalse(updated.Snapshot.Configuration.IPv6Enabled);
        Assert.AreEqual(capturedAt, updated.Snapshot.CapturedAt);
    }

    [TestMethod]
    public void AfterInsertion_WithoutReadableFinalSnapshotInvalidatesExistingAuthority()
    {
        RuleInventoryState state = Loaded(Inventory(new RuleListResponse(true, [Rule("old")], TestFirewallConfiguration.Enabled)));
        RuleInsertionResponse report = new(
            RuleInsertionOutcome.StateUncertain,
            FinalSnapshot: null,
            InsertedRule: null,
            Diagnostic: "unreadable");

        RuleInventoryState updated = state.MoveNext(new RuleInventoryTransition.InsertionCompleted(report));

        Assert.IsTrue(updated.IsStale);
        Assert.AreEqual(RuleSnapshotStaleReason.MutationOutcomeUnknown, updated.StaleReason);
        Assert.AreEqual("old", updated.Snapshot!.Rules[0].RuleId);
    }

    [TestMethod]
    public void AfterReorder_WithAuthoritativeFinalSnapshotReplacesLocalAuthority()
    {
        RuleInventoryState state = Loaded(Inventory(new RuleListResponse(true, [Rule("old")], TestFirewallConfiguration.Enabled)));
        RuleListResponse finalSnapshot = new(false, [Rule("new")], TestFirewallConfiguration.Enabled);
        RuleReorderResponse report = new(RuleReorderOutcome.PartiallyCompleted, finalSnapshot, [], [], [], Diagnostic: "partial");

        DateTimeOffset capturedAt = new(2026, 9, 25, 14, 30, 0, TimeSpan.Zero);
        RuleInventoryState updated = state.MoveNext(new RuleInventoryTransition.ReorderCompleted(report, capturedAt));

        Assert.IsTrue(updated.IsCurrent);
        Assert.IsNotNull(updated.Snapshot);
        Assert.IsFalse(updated.Snapshot.FirewallActive);
        Assert.AreEqual("new", updated.Snapshot.Rules[0].RuleId);
        Assert.AreEqual(capturedAt, updated.Snapshot.CapturedAt);
        Assert.IsNull(updated.StaleReason);
    }

    [TestMethod]
    public void AfterReorder_WithoutReadableFinalSnapshotInvalidatesExistingAuthority()
    {
        RuleInventoryState state = Loaded(Inventory(new RuleListResponse(true, [Rule("old")], TestFirewallConfiguration.Enabled)));
        RuleReorderResponse report = new(RuleReorderOutcome.StateUncertain, FinalSnapshot: null, [], [], [], Diagnostic: "unreadable");

        RuleInventoryState updated = state.MoveNext(new RuleInventoryTransition.ReorderCompleted(report));

        Assert.IsTrue(updated.IsStale);
        Assert.AreEqual(RuleSnapshotStaleReason.MutationOutcomeUnknown, updated.StaleReason);
        Assert.AreEqual("old", updated.Snapshot!.Rules[0].RuleId);
    }

    [TestMethod]
    public void MoveNext_RejectsOverlappingRefreshesAndCompletionWithoutRefresh()
    {
        RuleInventoryState refreshing = RuleInventoryState.Initial.MoveNext(new RuleInventoryTransition.RefreshStarted(RuleInventoryRefreshReason.Manual));

        Assert.ThrowsExactly<InvalidOperationException>(() => refreshing.MoveNext(new RuleInventoryTransition.RefreshStarted(RuleInventoryRefreshReason.Manual)));
        Assert.ThrowsExactly<InvalidOperationException>(() => RuleInventoryState.Initial.MoveNext(
            new RuleInventoryTransition.RefreshCompleted(Inventory(new RuleListResponse(true, [], TestFirewallConfiguration.Enabled)))));
    }

    [TestMethod]
    public void MoveNext_FailedPostMutationRefreshMarksSnapshotStaleAsCommitted()
    {
        RuleInventoryState state = Loaded(Inventory(new RuleListResponse(true, [Rule("old")], TestFirewallConfiguration.Enabled)));
        RuleInventoryState refreshing = state.MoveNext(new RuleInventoryTransition.RefreshStarted(RuleInventoryRefreshReason.AfterMutation));
        ClientError error = new(ClientErrorKind.Unavailable, "unavailable", Retryable: true);

        RuleInventoryState failed = refreshing.MoveNext(new RuleInventoryTransition.RefreshFailed(error));

        Assert.IsTrue(failed.IsStale);
        Assert.AreEqual(RuleSnapshotStaleReason.MutationCommitted, failed.StaleReason);
        Assert.AreSame(state.Snapshot, failed.Snapshot);
    }

    private static RuleInventoryState Loaded(RuleInventoryResponse response) => RuleInventoryState.Initial
        .MoveNext(new RuleInventoryTransition.RefreshStarted(RuleInventoryRefreshReason.Manual))
        .MoveNext(new RuleInventoryTransition.RefreshCompleted(response));

    private static RuleInventoryResponse Inventory(
        RuleListResponse firewall,
        IReadOnlyList<RuleMetadataItem>? metadata = null) => new()
    {
        Firewall = firewall,
        Metadata = metadata ?? [],
        CapturedAt = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero),
    };

    private static ListedFirewallRule Rule(string id) => new()
    {
        RuleId = id,
        DisplayNumber = 1,
        Parsed = true,
        RawLine = id,
        Rule = new FirewallRuleSpecification(),
    };
}
