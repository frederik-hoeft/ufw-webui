using Ufw.Client.Api;
using Ufw.Client.Rules;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Client.Tests.Rules;

[TestClass]
public sealed class RulesPageStateTests
{
    [TestMethod]
    public void CompleteRefresh_LoadsMetadataBySemanticIdentity()
    {
        Guid metadataId = Guid.CreateVersion7();
        Guid tagId = Guid.CreateVersion7();
        RuleInventoryResponse response = Inventory(
            new RuleListResponse(true, [Rule("shared"), Rule("shared")], TestFirewallConfiguration.Enabled),
            [new RuleMetadataItem
            {
                Id = metadataId,
                RuleId = "shared",
                Notes = "managed",
                Tags = [new RuleTagItem { Id = tagId, Name = "prod", Color = "#336699" }],
            }]);

        RulesPageState state = RulesPageState.CompleteRefresh(response);

        Assert.IsNotNull(state.Snapshot);
        Assert.HasCount(1, state.Snapshot.Metadata);
        Assert.AreEqual(metadataId, state.Snapshot.Metadata["shared"].Id);
        Assert.AreEqual("managed", state.Snapshot.Metadata["shared"].Notes);
        Assert.HasCount(1, state.Snapshot.Metadata["shared"].Tags);
        Assert.AreEqual(tagId, state.Snapshot.Metadata["shared"].Tags[0].Id);
        Assert.AreEqual("prod", state.Snapshot.Metadata["shared"].Tags[0].Name);
        Assert.AreEqual("#336699", state.Snapshot.Metadata["shared"].Tags[0].Color);
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
        RulesPageState state = RulesPageState.CompleteRefresh(response);
        RuleListResponse finalSnapshot = new(true, [Rule("keep"), Rule("new")], TestFirewallConfiguration.Enabled);
        RuleInsertionResponse report = new(RuleInsertionOutcome.Completed, finalSnapshot, Rule("new"), Diagnostic: null);

        RulesPageState updated = state.AfterInsertion(report);

        Assert.IsNotNull(updated.Snapshot);
        Assert.HasCount(1, updated.Snapshot.Metadata);
        Assert.IsTrue(updated.Snapshot.Metadata.ContainsKey("keep"));
        Assert.IsFalse(updated.Snapshot.Metadata.ContainsKey("remove"));
        Assert.IsFalse(updated.Snapshot.Metadata.ContainsKey("new"));
    }

    [TestMethod]
    public void AfterInsertion_WithAuthoritativeFinalSnapshotReplacesLocalAuthority()
    {
        RulesPageState state = RulesPageState.CompleteRefresh(Inventory(new RuleListResponse(true, [Rule("old")], TestFirewallConfiguration.Enabled)));
        RuleListResponse finalSnapshot = new(false, [Rule("old"), Rule("inserted")], TestFirewallConfiguration.Disabled);
        RuleInsertionResponse report = new(
            RuleInsertionOutcome.PreconditionFailed,
            finalSnapshot,
            InsertedRule: null,
            Diagnostic: "rejected");

        RulesPageState updated = state.AfterInsertion(report);

        Assert.IsTrue(updated.IsCurrent);
        Assert.IsNotNull(updated.Snapshot);
        Assert.IsFalse(updated.Snapshot.FirewallActive);
        Assert.HasCount(2, updated.Snapshot.Rules);
        Assert.AreEqual("inserted", updated.Snapshot.Rules[1].RuleId);
        Assert.IsFalse(updated.Snapshot.Configuration.IPv6Enabled);
    }

    [TestMethod]
    public void AfterInsertion_WithoutReadableFinalSnapshotInvalidatesExistingAuthority()
    {
        RulesPageState state = RulesPageState.CompleteRefresh(Inventory(new RuleListResponse(true, [Rule("old")], TestFirewallConfiguration.Enabled)));
        RuleInsertionResponse report = new(
            RuleInsertionOutcome.StateUncertain,
            FinalSnapshot: null,
            InsertedRule: null,
            Diagnostic: "unreadable");

        RulesPageState updated = state.AfterInsertion(report);

        Assert.IsTrue(updated.IsStale);
        Assert.AreEqual(RuleSnapshotStaleReason.MutationOutcomeUnknown, updated.StaleReason);
        Assert.AreEqual("old", updated.Snapshot!.Rules[0].RuleId);
    }

    [TestMethod]
    public void AfterReorder_WithAuthoritativeFinalSnapshotReplacesLocalAuthority()
    {
        RulesPageState state = RulesPageState.CompleteRefresh(Inventory(new RuleListResponse(true, [Rule("old")], TestFirewallConfiguration.Enabled)));
        RuleListResponse finalSnapshot = new(false, [Rule("new")], TestFirewallConfiguration.Enabled);
        RuleReorderResponse report = new(
            RuleReorderOutcome.PartiallyCompleted,
            finalSnapshot,
            [],
            [],
            [],
            Diagnostic: "partial");

        RulesPageState updated = state.AfterReorder(report);

        Assert.IsTrue(updated.IsCurrent);
        Assert.IsNotNull(updated.Snapshot);
        Assert.IsFalse(updated.Snapshot.FirewallActive);
        Assert.AreEqual("new", updated.Snapshot.Rules[0].RuleId);
        Assert.IsNull(updated.StaleReason);
    }

    [TestMethod]
    public void AfterReorder_WithoutReadableFinalSnapshotInvalidatesExistingAuthority()
    {
        RulesPageState state = RulesPageState.CompleteRefresh(Inventory(new RuleListResponse(true, [Rule("old")], TestFirewallConfiguration.Enabled)));
        RuleReorderResponse report = new(
            RuleReorderOutcome.StateUncertain,
            FinalSnapshot: null,
            [],
            [],
            [],
            Diagnostic: "unreadable");

        RulesPageState updated = state.AfterReorder(report);

        Assert.IsTrue(updated.IsStale);
        Assert.AreEqual(RuleSnapshotStaleReason.MutationOutcomeUnknown, updated.StaleReason);
        Assert.AreEqual("old", updated.Snapshot!.Rules[0].RuleId);
    }


    private static RuleInventoryResponse Inventory(
        RuleListResponse firewall,
        IReadOnlyList<RuleMetadataItem>? metadata = null) => new()
    {
        Firewall = firewall,
        Metadata = metadata ?? [],
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
