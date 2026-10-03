using Ufw.Web.Data.Access;
using Ufw.Web.Data.Access.Rules.Groups;
using Ufw.Web.Data.Access.Rules.Tags;
using Ufw.Shared.Management.Rules;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Data;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Model.V1.RuleMetadata;
using Ufw.Web.Data;
using Ufw.Web.Tests.Data;
using Ufw.Web.Data.Model;
using Ufw.Web.Services.Rules;
using Wkg.AspNetCore.Transactions;
using Wkg.AspNetCore.Transactions.Configuration;
using Wkg.EntityFrameworkCore.Configuration;

namespace Ufw.Web.Tests.Rules;

[TestClass]
public sealed class RuleMetadataServiceTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task UpdateAndInventory_NormalizeMetadataAndRetainItWhenRuleDisappearsOutOfBandAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:live", "sha256:other");
        RuleTagItem production = await host.CreateTagAsync(" Production ", "#12ab34", TestContext.CancellationToken);
        RuleTagItem ssh = await host.CreateTagAsync("SSH", "#AABBCC", TestContext.CancellationToken);

        RuleMetadataUpdateResult updated = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest
            {
                Notes = " managed by platform ",
                TagIds = [production.Id, production.Id, ssh.Id],
            },
            TestContext.CancellationToken);

        Assert.AreEqual(RuleMetadataUpdateOutcome.Success, updated.Outcome);
        Assert.IsNotNull(updated.Response?.Metadata);
        Assert.AreEqual('7', updated.Response.Metadata.Id.ToString("D")[14]);
        Assert.AreEqual("managed by platform", updated.Response.Metadata.Notes);
        Assert.HasCount(2, updated.Response.Metadata.Tags);
        Assert.AreEqual("Production", updated.Response.Metadata.Tags[0].Name);
        Assert.AreEqual("#12AB34", updated.Response.Metadata.Tags[0].Color);
        Assert.AreEqual("SSH", updated.Response.Metadata.Tags[1].Name);
        Assert.AreEqual(1, await host.MetadataRowCountAsync(TestContext.CancellationToken));
        Assert.AreEqual(2, await host.MetadataTagRowCountAsync(TestContext.CancellationToken));
        Assert.AreEqual(2, await host.RuleTagRowCountAsync(TestContext.CancellationToken));

        RuleInventoryResponse inventory = await host.Inventory.GetAsync(TestContext.CancellationToken);
        Assert.HasCount(2, inventory.Firewall.Rules);
        Assert.HasCount(1, inventory.Metadata);
        Assert.AreEqual("sha256:live", inventory.Metadata[0].RuleId);
        Assert.AreEqual(updated.Response.Metadata.Id, inventory.Metadata[0].Id);

        host.SetRules("sha256:other");
        RuleInventoryResponse afterExternalRemoval = await host.Inventory.GetAsync(TestContext.CancellationToken);

        Assert.IsEmpty(afterExternalRemoval.Metadata);
        Assert.AreEqual(1, await host.MetadataRowCountAsync(TestContext.CancellationToken));
        Assert.AreEqual(2, await host.RuleTagRowCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Inventory_ReadWithoutMetadataIsSideEffectFreeAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:live");

        RuleInventoryResponse inventory = await host.Inventory.GetAsync(TestContext.CancellationToken);

        Assert.HasCount(1, inventory.Firewall.Rules);
        Assert.IsEmpty(inventory.Metadata);
        Assert.AreEqual(0, await host.MetadataRowCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Update_RejectsMissingRuleInvalidMetadataAndUnknownTagsWithoutPersistingAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:live");

        RuleMetadataUpdateResult missing = await host.Metadata.UpdateAsync(
            "sha256:missing",
            new UpdateRuleMetadataRequest { Notes = "edge" },
            TestContext.CancellationToken);
        RuleMetadataUpdateResult invalid = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { TagIds = Enumerable.Repeat(Guid.CreateVersion7(), 33).ToArray() },
            TestContext.CancellationToken);
        RuleMetadataUpdateResult unknownTag = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { TagIds = [Guid.CreateVersion7()] },
            TestContext.CancellationToken);

        Assert.AreEqual(RuleMetadataUpdateOutcome.RuleNotFound, missing.Outcome);
        Assert.AreEqual(RuleMetadataUpdateOutcome.InvalidMetadata, invalid.Outcome);
        Assert.AreEqual(RuleMetadataUpdateOutcome.TagNotFound, unknownTag.Outcome);
        Assert.AreEqual(0, await host.MetadataRowCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Update_EmptyMetadataRemovesRelationButPreservesReusableTagAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:live");
        RuleTagItem production = await host.CreateTagAsync("prod", "#112233", TestContext.CancellationToken);
        _ = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { TagIds = [production.Id] },
            TestContext.CancellationToken);

        RuleMetadataUpdateResult cleared = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { Notes = "  ", TagIds = [] },
            TestContext.CancellationToken);

        Assert.AreEqual(RuleMetadataUpdateOutcome.Success, cleared.Outcome);
        Assert.IsNull(cleared.Response?.Metadata);
        Assert.AreEqual(0, await host.MetadataRowCountAsync(TestContext.CancellationToken));
        Assert.AreEqual(0, await host.MetadataTagRowCountAsync(TestContext.CancellationToken));
        Assert.AreEqual(1, await host.RuleTagRowCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task RuleTags_UseUuidV7IdentityAndCannotBeDeletedWhileAttachedAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:live");

        DataMutationResult created = await host.Tags.CreateAsync("Production", "#12AB34", TestContext.CancellationToken);

        Assert.IsTrue(created.IsSuccess);
        RuleTagItem tag = (await host.Tags.GetAsync(TestContext.CancellationToken)).Single();
        Assert.AreEqual('7', tag.Id.ToString("D")[14]);
        Assert.AreEqual("Production", tag.Name);
        Assert.AreEqual("#12AB34", tag.Color);

        DataMutationResult duplicate = await host.Tags.CreateAsync("PRODUCTION", "#FFFFFF", TestContext.CancellationToken);
        Assert.IsInstanceOfType<DataMutationUniqueConflictError>(duplicate.Error);

        DataMutationResult updated = await host.Tags.UpdateAsync(tag.Id, "Prod", "#0011AA", TestContext.CancellationToken);
        Assert.IsTrue(updated.IsSuccess);
        RuleTagItem renamed = (await host.Tags.GetAsync(TestContext.CancellationToken)).Single();
        Assert.AreEqual(tag.Id, renamed.Id);
        Assert.AreEqual("Prod", renamed.Name);
        Assert.AreEqual("#0011AA", renamed.Color);

        _ = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { TagIds = [tag.Id] },
            TestContext.CancellationToken);

        DataMutationResult inUse = await host.Tags.DeleteAsync(tag.Id, TestContext.CancellationToken);
        Assert.IsInstanceOfType<DataMutationReferenceConflictError>(inUse.Error);
        Assert.AreEqual(1, await host.RuleTagRowCountAsync(TestContext.CancellationToken));

        _ = await host.Metadata.UpdateAsync("sha256:live", new UpdateRuleMetadataRequest(), TestContext.CancellationToken);
        DataMutationResult deleted = await host.Tags.DeleteAsync(tag.Id, TestContext.CancellationToken);

        Assert.IsTrue(deleted.IsSuccess);
        Assert.IsEmpty(await host.Tags.GetAsync(TestContext.CancellationToken));
        Assert.AreEqual(0, await host.RuleTagRowCountAsync(TestContext.CancellationToken));
    }


    [TestMethod]
    public async Task RuleGroups_SupportEmptyGroupLifecycleAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);

        DataMutationResult created = await host.Groups.CreateAsync("Platform", "managed rules", TestContext.CancellationToken);

        Assert.IsTrue(created.IsSuccess);
        RuleGroupItem group = (await host.Groups.GetAsync(TestContext.CancellationToken)).Single();
        Assert.AreEqual('7', group.Id.ToString("D")[14]);
        Assert.AreEqual("Platform", group.Name);
        Assert.AreEqual("managed rules", group.Comment);
        Assert.IsEmpty(group.RuleIds);

        DataMutationResult duplicate = await host.Groups.CreateAsync("PLATFORM", null, TestContext.CancellationToken);
        Assert.IsInstanceOfType<DataMutationUniqueConflictError>(duplicate.Error);

        DataMutationResult updated = await host.Groups.UpdateAsync(group.Id, "Core", null, TestContext.CancellationToken);
        Assert.IsTrue(updated.IsSuccess);
        RuleGroupItem renamed = (await host.Groups.GetAsync(TestContext.CancellationToken)).Single();
        Assert.AreEqual(group.Id, renamed.Id);
        Assert.AreEqual("Core", renamed.Name);
        Assert.IsNull(renamed.Comment);

        DataMutationResult deleted = await host.Groups.DeleteAsync(group.Id, TestContext.CancellationToken);
        Assert.IsTrue(deleted.IsSuccess);
        Assert.IsEmpty(await host.Groups.GetAsync(TestContext.CancellationToken));
        Assert.AreEqual(0, await host.RuleGroupRowCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Update_GroupOnlyMetadataSupportsSingleMembershipAndRejectsUnknownGroupWithoutChangingMembershipAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:live");
        RuleGroupItem first = await host.CreateGroupAsync("First", null, TestContext.CancellationToken);
        RuleGroupItem second = await host.CreateGroupAsync("Second", "secondary", TestContext.CancellationToken);

        RuleMetadataUpdateResult assigned = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { GroupId = first.Id },
            TestContext.CancellationToken);

        Assert.AreEqual(RuleMetadataUpdateOutcome.Success, assigned.Outcome);
        Assert.IsNotNull(assigned.Response?.Metadata?.Group);
        Assert.AreEqual(first.Id, assigned.Response.Metadata.Group.Id);
        Assert.IsNull(assigned.Response.Metadata.Notes);
        Assert.IsEmpty(assigned.Response.Metadata.Tags);
        Assert.AreEqual(1, await host.MetadataRowCountAsync(TestContext.CancellationToken));

        RuleMetadataUpdateResult unknown = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { GroupId = Guid.CreateVersion7() },
            TestContext.CancellationToken);
        Assert.AreEqual(RuleMetadataUpdateOutcome.GroupNotFound, unknown.Outcome);

        RuleInventoryResponse afterRejectedUpdate = await host.Inventory.GetAsync(TestContext.CancellationToken);
        RuleGroupSummary? effectiveGroup = afterRejectedUpdate.Metadata.Single().Group;
        Assert.IsNotNull(effectiveGroup);
        Assert.AreEqual(first.Id, effectiveGroup.Id);
        Assert.AreEqual("First", effectiveGroup.Name);
        Assert.IsNull(effectiveGroup.Comment);

        RuleMetadataUpdateResult reassigned = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { GroupId = second.Id },
            TestContext.CancellationToken);
        Assert.AreEqual(RuleMetadataUpdateOutcome.Success, reassigned.Outcome);
        Assert.AreEqual(second.Id, reassigned.Response!.Metadata!.Group?.Id);

        IReadOnlyList<RuleGroupItem> groups = await host.Groups.GetAsync(TestContext.CancellationToken);
        Assert.IsEmpty(groups.Single(candidate => candidate.Id == first.Id).RuleIds);
        CollectionAssert.AreEqual(new[] { "sha256:live" }, groups.Single(candidate => candidate.Id == second.Id).RuleIds.ToArray());
    }

    [TestMethod]
    public async Task RuleGroups_CannotDeleteInUseGroupAndReconciliationPreservesGroupAfterRemovingOrphanMembershipAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:orphan");
        RuleGroupItem group = await host.CreateGroupAsync("Batch", "kept even when empty", TestContext.CancellationToken);
        RuleMetadataItem metadata = (await host.Metadata.UpdateAsync(
            "sha256:orphan",
            new UpdateRuleMetadataRequest { GroupId = group.Id },
            TestContext.CancellationToken)).Response!.Metadata!;

        DataMutationResult inUse = await host.Groups.DeleteAsync(group.Id, TestContext.CancellationToken);
        Assert.IsInstanceOfType<DataMutationReferenceConflictError>(inUse.Error);

        host.SetRules();
        RuleMetadataReconciliationResponse cleaned = await host.Reconciliation.CleanupAsync(
            new CleanupRuleMetadataRequest { MetadataIds = [metadata.Id] },
            TestContext.CancellationToken);

        Assert.AreEqual(1, cleaned.RemovedCount);
        Assert.AreEqual(0, await host.MetadataRowCountAsync(TestContext.CancellationToken));
        IReadOnlyList<RuleGroupItem> groups = await host.Groups.GetAsync(TestContext.CancellationToken);
        RuleGroupItem preserved = groups.Single();
        Assert.AreEqual(group.Id, preserved.Id);
        Assert.IsEmpty(preserved.RuleIds);
    }

    [TestMethod]
    public async Task Reconciliation_DiscoveryIsSideEffectFreeAndReturnsOnlyUnmatchedMetadataAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:live", "sha256:orphan");
        _ = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { Notes = "still attached" },
            TestContext.CancellationToken);
        _ = await host.Metadata.UpdateAsync(
            "sha256:orphan",
            new UpdateRuleMetadataRequest { Notes = "review me" },
            TestContext.CancellationToken);
        host.SetRules("sha256:live");

        RuleMetadataReconciliationResponse response = await host.Reconciliation.GetAsync(TestContext.CancellationToken);

        Assert.AreEqual(0, response.RemovedCount);
        Assert.HasCount(1, response.Orphans);
        Assert.AreEqual("sha256:orphan", response.Orphans[0].RuleId);
        Assert.AreEqual("review me", response.Orphans[0].Notes);
        Assert.AreEqual(2, await host.MetadataRowCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Reconciliation_CleanupRemovesOnlySelectedRecordsThatAreStillUnmatchedAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:reattached", "sha256:remove", "sha256:keep");
        RuleMetadataItem reattached = (await host.Metadata.UpdateAsync(
            "sha256:reattached",
            new UpdateRuleMetadataRequest { Notes = "reattached" },
            TestContext.CancellationToken)).Response!.Metadata!;
        RuleMetadataItem remove = (await host.Metadata.UpdateAsync(
            "sha256:remove",
            new UpdateRuleMetadataRequest { Notes = "remove" },
            TestContext.CancellationToken)).Response!.Metadata!;
        RuleMetadataItem keep = (await host.Metadata.UpdateAsync(
            "sha256:keep",
            new UpdateRuleMetadataRequest { Notes = "keep" },
            TestContext.CancellationToken)).Response!.Metadata!;
        host.SetRules();

        RuleMetadataReconciliationResponse discovered = await host.Reconciliation.GetAsync(TestContext.CancellationToken);
        Assert.HasCount(3, discovered.Orphans);

        host.SetRules("sha256:reattached");
        RuleMetadataReconciliationResponse cleaned = await host.Reconciliation.CleanupAsync(
            new CleanupRuleMetadataRequest { MetadataIds = [reattached.Id, remove.Id] },
            TestContext.CancellationToken);

        Assert.AreEqual(1, cleaned.RemovedCount);
        Assert.HasCount(1, cleaned.Orphans);
        Assert.AreEqual(keep.Id, cleaned.Orphans[0].Id);
        Assert.AreEqual(2, await host.MetadataRowCountAsync(TestContext.CancellationToken));

        RuleInventoryResponse inventory = await host.Inventory.GetAsync(TestContext.CancellationToken);
        Assert.HasCount(1, inventory.Metadata);
        Assert.AreEqual(reattached.Id, inventory.Metadata[0].Id);
    }

    [TestMethod]
    public async Task ReconcileBatchDelete_RemovesOnlyConfirmedSemanticIdsAbsentFromFinalSnapshotAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:delete", "sha256:duplicate", "sha256:keep");
        _ = await host.Metadata.UpdateAsync("sha256:delete", new UpdateRuleMetadataRequest { Notes = "delete" }, TestContext.CancellationToken);
        _ = await host.Metadata.UpdateAsync("sha256:duplicate", new UpdateRuleMetadataRequest { Notes = "duplicate" }, TestContext.CancellationToken);
        _ = await host.Metadata.UpdateAsync("sha256:keep", new UpdateRuleMetadataRequest { Notes = "keep" }, TestContext.CancellationToken);
        RuleListResponse finalSnapshot = new(
            true,
            [
                Listed("sha256:duplicate", 1),
                Listed("sha256:keep", 2),
            ],
            TestFirewallConfiguration.Enabled);
        RuleBatchDeleteResponse response = new(
            RuleBatchDeleteOutcome.PartiallyCompleted,
            finalSnapshot,
            [
                new RuleBatchDeleteOperationResponse(0, "sha256:delete", RuleBatchDeleteOperationOutcome.DeletedAfterProcessFailure, "delete reported failure after mutation"),
                new RuleBatchDeleteOperationResponse(1, "sha256:duplicate", RuleBatchDeleteOperationOutcome.Deleted, null),
                new RuleBatchDeleteOperationResponse(2, "sha256:keep", RuleBatchDeleteOperationOutcome.Failed, "not deleted"),
            ],
            [2],
            "partial");

        await host.Metadata.ReconcileBatchDeleteAsync(response, TestContext.CancellationToken);

        Assert.AreEqual(2, await host.MetadataRowCountAsync(TestContext.CancellationToken));
        host.SetRules("sha256:duplicate", "sha256:keep");
        RuleInventoryResponse inventory = await host.Inventory.GetAsync(TestContext.CancellationToken);
        CollectionAssert.AreEquivalent(new[] { "sha256:duplicate", "sha256:keep" }, inventory.Metadata.Select(static item => item.RuleId).ToArray());
    }

    [TestMethod]
    public async Task ReconcileBatchDelete_UncertainFinalState_DoesNotDeleteMetadataAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:target");
        _ = await host.Metadata.UpdateAsync("sha256:target", new UpdateRuleMetadataRequest { Notes = "keep until known" }, TestContext.CancellationToken);
        RuleBatchDeleteResponse response = new(
            RuleBatchDeleteOutcome.StateUncertain,
            null,
            [new RuleBatchDeleteOperationResponse(0, "sha256:target", RuleBatchDeleteOperationOutcome.StateUncertain, "unknown")],
            [0],
            "unknown");

        await host.Metadata.ReconcileBatchDeleteAsync(response, TestContext.CancellationToken);

        Assert.AreEqual(1, await host.MetadataRowCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReconcileReplacement_RekeysMetadataWhenOriginalIdentityIsNoLongerLiveAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        FirewallRuleSpecification originalRule = Rule("22");
        FirewallRuleSpecification replacementRule = Rule("443");
        string originalRuleId = RuleIdentity.Compute(originalRule);
        string replacementRuleId = RuleIdentity.Compute(replacementRule);
        host.SetRules(originalRuleId);
        RuleTagItem tag = await host.CreateTagAsync("prod", "#112233", TestContext.CancellationToken);
        RuleGroupItem group = await host.CreateGroupAsync("platform", "managed", TestContext.CancellationToken);
        RuleMetadataUpdateResult saved = await host.Metadata.UpdateAsync(
            originalRuleId,
            new UpdateRuleMetadataRequest { Notes = "source", TagIds = [tag.Id], GroupId = group.Id },
            TestContext.CancellationToken);
        Guid sourceMetadataId = saved.Response!.Metadata!.Id;
        ReplaceRuleRequest request = ReplacementRequest(originalRuleId, replacementRule);
        RuleReplacementResponse response = CompletedReplacement(replacementRuleId, replacementRule, replacementRuleId);

        RuleReplacementMetadataReconciliationOutcome outcome = await host.Metadata.ReconcileReplacementAsync(request, response, TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementMetadataReconciliationOutcome.Completed, outcome);
        host.SetRules(replacementRuleId);
        RuleMetadataItem metadata = (await host.Inventory.GetAsync(TestContext.CancellationToken)).Metadata.Single();
        Assert.AreEqual(sourceMetadataId, metadata.Id);
        Assert.AreEqual(replacementRuleId, metadata.RuleId);
        Assert.AreEqual("source", metadata.Notes);
        Assert.AreEqual(tag.Id, metadata.Tags.Single().Id);
        Assert.AreEqual(group.Id, metadata.Group?.Id);
        Assert.AreEqual(1, await host.MetadataRowCountAsync(TestContext.CancellationToken));
        Assert.AreEqual(1, await host.MetadataTagRowCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReconcileReplacement_CopiesMetadataWhenOriginalIdentityRemainsLiveAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        FirewallRuleSpecification originalRule = Rule("22");
        FirewallRuleSpecification replacementRule = Rule("443");
        string originalRuleId = RuleIdentity.Compute(originalRule);
        string replacementRuleId = RuleIdentity.Compute(replacementRule);
        host.SetRules(originalRuleId);
        RuleTagItem tag = await host.CreateTagAsync("prod", "#112233", TestContext.CancellationToken);
        RuleGroupItem group = await host.CreateGroupAsync("platform", "managed", TestContext.CancellationToken);
        RuleMetadataUpdateResult saved = await host.Metadata.UpdateAsync(
            originalRuleId,
            new UpdateRuleMetadataRequest { Notes = "shared", TagIds = [tag.Id], GroupId = group.Id },
            TestContext.CancellationToken);
        Guid originalMetadataId = saved.Response!.Metadata!.Id;
        ReplaceRuleRequest request = ReplacementRequest(originalRuleId, replacementRule);
        RuleReplacementResponse response = CompletedReplacement(replacementRuleId, replacementRule, originalRuleId, replacementRuleId);

        RuleReplacementMetadataReconciliationOutcome outcome = await host.Metadata.ReconcileReplacementAsync(request, response, TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementMetadataReconciliationOutcome.Completed, outcome);
        host.SetRules(originalRuleId, replacementRuleId);
        RuleMetadataItem[] metadata = [.. (await host.Inventory.GetAsync(TestContext.CancellationToken)).Metadata.OrderBy(static item => item.RuleId, StringComparer.Ordinal)];
        Assert.HasCount(2, metadata);
        RuleMetadataItem original = metadata.Single(item => item.RuleId == originalRuleId);
        RuleMetadataItem replacement = metadata.Single(item => item.RuleId == replacementRuleId);
        Assert.AreEqual(originalMetadataId, original.Id);
        Assert.AreNotEqual(originalMetadataId, replacement.Id);
        Assert.AreEqual("shared", replacement.Notes);
        Assert.AreEqual(tag.Id, replacement.Tags.Single().Id);
        Assert.AreEqual(group.Id, replacement.Group?.Id);
        Assert.AreEqual(2, await host.MetadataRowCountAsync(TestContext.CancellationToken));
        Assert.AreEqual(2, await host.MetadataTagRowCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReconcileReplacement_SameIdentityLeavesMetadataIdentityUntouchedAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        FirewallRuleSpecification originalRule = Rule("22");
        FirewallRuleSpecification commentUpdate = Rule("22");
        commentUpdate.Comment = "updated";
        string ruleId = RuleIdentity.Compute(originalRule);
        host.SetRules(ruleId);
        RuleMetadataUpdateResult saved = await host.Metadata.UpdateAsync(
            ruleId,
            new UpdateRuleMetadataRequest { Notes = "keep" },
            TestContext.CancellationToken);
        Guid metadataId = saved.Response!.Metadata!.Id;
        ReplaceRuleRequest request = ReplacementRequest(ruleId, commentUpdate);
        RuleReplacementResponse response = CompletedReplacement(ruleId, commentUpdate, ruleId);

        RuleReplacementMetadataReconciliationOutcome outcome = await host.Metadata.ReconcileReplacementAsync(request, response, TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementMetadataReconciliationOutcome.Completed, outcome);
        RuleMetadataItem metadata = (await host.Inventory.GetAsync(TestContext.CancellationToken)).Metadata.Single();
        Assert.AreEqual(metadataId, metadata.Id);
        Assert.AreEqual(ruleId, metadata.RuleId);
        Assert.AreEqual("keep", metadata.Notes);
    }

    [TestMethod]
    public async Task ReconcileReplacement_TargetCollisionFavorsSourceMetadataAndPreservesSourceIdentityAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        FirewallRuleSpecification originalRule = Rule("22");
        FirewallRuleSpecification replacementRule = Rule("443");
        string originalRuleId = RuleIdentity.Compute(originalRule);
        string replacementRuleId = RuleIdentity.Compute(replacementRule);
        host.SetRules(originalRuleId, replacementRuleId);
        RuleMetadataUpdateResult staleTarget = await host.Metadata.UpdateAsync(
            replacementRuleId,
            new UpdateRuleMetadataRequest { Notes = "stale target" },
            TestContext.CancellationToken);
        RuleMetadataUpdateResult source = await host.Metadata.UpdateAsync(
            originalRuleId,
            new UpdateRuleMetadataRequest { Notes = "source wins" },
            TestContext.CancellationToken);
        Guid staleTargetId = staleTarget.Response!.Metadata!.Id;
        Guid sourceId = source.Response!.Metadata!.Id;
        ReplaceRuleRequest request = ReplacementRequest(originalRuleId, replacementRule);
        RuleReplacementResponse response = CompletedReplacement(replacementRuleId, replacementRule, replacementRuleId);

        RuleReplacementMetadataReconciliationOutcome outcome = await host.Metadata.ReconcileReplacementAsync(request, response, TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementMetadataReconciliationOutcome.Completed, outcome);
        host.SetRules(replacementRuleId);
        RuleMetadataItem metadata = (await host.Inventory.GetAsync(TestContext.CancellationToken)).Metadata.Single();
        Assert.AreEqual(sourceId, metadata.Id);
        Assert.AreNotEqual(staleTargetId, metadata.Id);
        Assert.AreEqual("source wins", metadata.Notes);
        Assert.AreEqual(1, await host.MetadataRowCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReconcileReplacement_CopyOverTargetCollisionUsesSourceMetadataAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        FirewallRuleSpecification originalRule = Rule("22");
        FirewallRuleSpecification replacementRule = Rule("443");
        string originalRuleId = RuleIdentity.Compute(originalRule);
        string replacementRuleId = RuleIdentity.Compute(replacementRule);
        host.SetRules(originalRuleId, replacementRuleId);
        RuleMetadataUpdateResult staleTarget = await host.Metadata.UpdateAsync(
            replacementRuleId,
            new UpdateRuleMetadataRequest { Notes = "stale target" },
            TestContext.CancellationToken);
        RuleMetadataUpdateResult source = await host.Metadata.UpdateAsync(
            originalRuleId,
            new UpdateRuleMetadataRequest { Notes = "source copy" },
            TestContext.CancellationToken);
        Guid staleTargetId = staleTarget.Response!.Metadata!.Id;
        Guid sourceId = source.Response!.Metadata!.Id;
        ReplaceRuleRequest request = ReplacementRequest(originalRuleId, replacementRule);
        RuleReplacementResponse response = CompletedReplacement(replacementRuleId, replacementRule, originalRuleId, replacementRuleId);

        RuleReplacementMetadataReconciliationOutcome outcome = await host.Metadata.ReconcileReplacementAsync(request, response, TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementMetadataReconciliationOutcome.Completed, outcome);
        RuleMetadataItem[] metadata = [.. (await host.Inventory.GetAsync(TestContext.CancellationToken)).Metadata.OrderBy(static item => item.RuleId, StringComparer.Ordinal)];
        Assert.HasCount(2, metadata);
        RuleMetadataItem original = metadata.Single(item => item.RuleId == originalRuleId);
        RuleMetadataItem replacement = metadata.Single(item => item.RuleId == replacementRuleId);
        Assert.AreEqual(sourceId, original.Id);
        Assert.AreNotEqual(sourceId, replacement.Id);
        Assert.AreNotEqual(staleTargetId, replacement.Id);
        Assert.AreEqual("source copy", replacement.Notes);
    }

    [TestMethod]
    public async Task ReconcileReplacement_NoSourceMetadataRemovesStaleTargetMetadataAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        FirewallRuleSpecification originalRule = Rule("22");
        FirewallRuleSpecification replacementRule = Rule("443");
        string originalRuleId = RuleIdentity.Compute(originalRule);
        string replacementRuleId = RuleIdentity.Compute(replacementRule);
        host.SetRules(replacementRuleId);
        _ = await host.Metadata.UpdateAsync(
            replacementRuleId,
            new UpdateRuleMetadataRequest { Notes = "must not resurrect" },
            TestContext.CancellationToken);
        host.SetRules(originalRuleId);
        ReplaceRuleRequest request = ReplacementRequest(originalRuleId, replacementRule);
        RuleReplacementResponse response = CompletedReplacement(replacementRuleId, replacementRule, replacementRuleId);

        RuleReplacementMetadataReconciliationOutcome outcome = await host.Metadata.ReconcileReplacementAsync(request, response, TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementMetadataReconciliationOutcome.Completed, outcome);
        host.SetRules(replacementRuleId);
        Assert.IsEmpty((await host.Inventory.GetAsync(TestContext.CancellationToken)).Metadata);
        Assert.AreEqual(0, await host.MetadataRowCountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task ReconcileReplacement_NonCompletedFirewallOutcomeLeavesMetadataUntouchedAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        FirewallRuleSpecification originalRule = Rule("22");
        FirewallRuleSpecification replacementRule = Rule("443");
        string originalRuleId = RuleIdentity.Compute(originalRule);
        host.SetRules(originalRuleId);
        RuleMetadataUpdateResult saved = await host.Metadata.UpdateAsync(
            originalRuleId,
            new UpdateRuleMetadataRequest { Notes = "keep" },
            TestContext.CancellationToken);
        Guid metadataId = saved.Response!.Metadata!.Id;
        RuleReplacementResponse response = new(RuleReplacementOutcome.PartiallyCompleted, null, null, RecoveryOutcome: null, Diagnostic: "partial");

        RuleReplacementMetadataReconciliationOutcome outcome = await host.Metadata.ReconcileReplacementAsync(
            ReplacementRequest(originalRuleId, replacementRule),
            response,
            TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementMetadataReconciliationOutcome.NotAttempted, outcome);
        RuleMetadataItem metadata = (await host.Inventory.GetAsync(TestContext.CancellationToken)).Metadata.Single();
        Assert.AreEqual(metadataId, metadata.Id);
        Assert.AreEqual("keep", metadata.Notes);
    }

    [TestMethod]
    public async Task ReconcileReplacement_DatabaseFailureRollsBackTargetCollisionAndReportsFailureAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        FirewallRuleSpecification originalRule = Rule("22");
        FirewallRuleSpecification replacementRule = Rule("443");
        string originalRuleId = RuleIdentity.Compute(originalRule);
        string replacementRuleId = RuleIdentity.Compute(replacementRule);
        host.SetRules(originalRuleId, replacementRuleId);
        RuleMetadataUpdateResult staleTarget = await host.Metadata.UpdateAsync(
            replacementRuleId,
            new UpdateRuleMetadataRequest { Notes = "stale target" },
            TestContext.CancellationToken);
        RuleMetadataUpdateResult source = await host.Metadata.UpdateAsync(
            originalRuleId,
            new UpdateRuleMetadataRequest { Notes = "source" },
            TestContext.CancellationToken);
        Guid staleTargetId = staleTarget.Response!.Metadata!.Id;
        Guid sourceId = source.Response!.Metadata!.Id;
        await host.ExecuteSqlAsync(
            $"""
            CREATE TRIGGER FailRuleMetadataReplacement
            BEFORE UPDATE OF "RuleId" ON "RuleMetadata"
            WHEN NEW."RuleId" = '{replacementRuleId}'
            BEGIN
                SELECT RAISE(ABORT, 'forced replacement failure');
            END;
            """,
            TestContext.CancellationToken);

        RuleReplacementMetadataReconciliationOutcome outcome = await host.Metadata.ReconcileReplacementAsync(
            ReplacementRequest(originalRuleId, replacementRule),
            CompletedReplacement(replacementRuleId, replacementRule, replacementRuleId),
            TestContext.CancellationToken);

        Assert.AreEqual(RuleReplacementMetadataReconciliationOutcome.Failed, outcome);
        Assert.AreEqual(2, await host.MetadataRowCountAsync(TestContext.CancellationToken));
        host.SetRules(originalRuleId, replacementRuleId);
        RuleMetadataItem[] metadata = [.. (await host.Inventory.GetAsync(TestContext.CancellationToken)).Metadata.OrderBy(static item => item.RuleId, StringComparer.Ordinal)];
        Assert.HasCount(2, metadata);
        Assert.AreEqual(sourceId, metadata.Single(item => item.RuleId == originalRuleId).Id);
        Assert.AreEqual("source", metadata.Single(item => item.RuleId == originalRuleId).Notes);
        Assert.AreEqual(staleTargetId, metadata.Single(item => item.RuleId == replacementRuleId).Id);
        Assert.AreEqual("stale target", metadata.Single(item => item.RuleId == replacementRuleId).Notes);
    }

    [TestMethod]
    public async Task RemoveForDeletedRule_RemovesMetadataForInBandDeletionAsync()
    {
        await using TestHost host = await TestHost.CreateAsync(TestContext.CancellationToken);
        host.SetRules("sha256:live");
        _ = await host.Metadata.UpdateAsync(
            "sha256:live",
            new UpdateRuleMetadataRequest { Notes = "temporary" },
            TestContext.CancellationToken);

        await host.Metadata.RemoveForDeletedRuleAsync("sha256:live", TestContext.CancellationToken);

        Assert.AreEqual(0, await host.MetadataRowCountAsync(TestContext.CancellationToken));
    }

    private static FirewallRuleSpecification Rule(string destinationPort) => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.IPv4,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        DestinationPorts = destinationPort,
    };

    private static ReplaceRuleRequest ReplacementRequest(string originalRuleId, FirewallRuleSpecification replacementRule)
    {
        ReplaceRulePayload payload = new()
        {
            BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: true, []),
            TargetOccurrenceId = 0,
            OriginalRuleId = originalRuleId,
            ReplacementRule = replacementRule,
        };
        return new ReplaceRuleRequest
        {
            Version = IntentProtocol.VERSION,
            DeploymentId = "deployment",
            KeyId = "sha256:key",
            IssuedAtUnix = 1,
            Nonce = "nonce",
            Operation = IntentOperations.REPLACE_RULE,
            Payload = System.Text.Json.JsonSerializer.SerializeToElement(payload, MessageJsonSerializerContext.Default.ReplaceRulePayload),
            Signature = "signature",
        };
    }

    private static RuleReplacementResponse CompletedReplacement(string replacementRuleId, FirewallRuleSpecification replacementRule, params string[] finalRuleIds)
    {
        ListedFirewallRule replacement = new()
        {
            RuleId = replacementRuleId,
            DisplayNumber = Array.IndexOf(finalRuleIds, replacementRuleId) + 1,
            Parsed = true,
            RawLine = replacementRuleId,
            Rule = replacementRule,
        };
        ListedFirewallRule[] finalRules = [.. finalRuleIds.Select((ruleId, index) => string.Equals(ruleId, replacementRuleId, StringComparison.Ordinal)
            ? new ListedFirewallRule
            {
                RuleId = replacement.RuleId,
                DisplayNumber = index + 1,
                Parsed = replacement.Parsed,
                RawLine = replacement.RawLine,
                Rule = replacement.Rule,
            }
            : Listed(ruleId, index + 1))];
        return new RuleReplacementResponse(
            RuleReplacementOutcome.Completed,
            new RuleListResponse(true, finalRules, TestFirewallConfiguration.Enabled),
            replacement,
            RecoveryOutcome: null,
            Diagnostic: null);
    }

    private static ListedFirewallRule Listed(string ruleId, int displayNumber) => new()
    {
        RuleId = ruleId,
        DisplayNumber = displayNumber,
        Parsed = true,
        RawLine = ruleId,
        Rule = new FirewallRuleSpecification { AddressFamily = FirewallAddressFamily.IPv4 },
    };

    private sealed class TestHost : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _services;
        private readonly AsyncServiceScope _scope;
        private readonly TestDaemonRuleSource _daemon;
        private readonly ApplicationDbContext _context;

        private TestHost(
            SqliteConnection connection,
            ServiceProvider services,
            AsyncServiceScope scope,
            TestDaemonRuleSource daemon,
            ApplicationDbContext context,
            RuleMetadataService metadata,
            RuleInventoryService inventory,
            RuleMetadataReconciliationService reconciliation,
            RuleGroupDataAccess groups,
            RuleTagDataAccess tags)
        {
            _connection = connection;
            _services = services;
            _scope = scope;
            _daemon = daemon;
            _context = context;
            Metadata = metadata;
            Inventory = inventory;
            Reconciliation = reconciliation;
            Groups = groups;
            Tags = tags;
        }

        public RuleMetadataService Metadata { get; }

        public RuleInventoryService Inventory { get; }

        public RuleMetadataReconciliationService Reconciliation { get; }

        public RuleGroupDataAccess Groups { get; }

        public RuleTagDataAccess Tags { get; }

        public static async Task<TestHost> CreateAsync(CancellationToken cancellationToken)
        {
            SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync(cancellationToken);
            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton<IModelLoader, SqliteApplicationModelLoader>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddTransactionManagement<ApplicationDbContext>(options =>
                options.UseIsolationLevel(IsolationLevel.ReadCommitted));

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync(cancellationToken);

            TestDaemonRuleSource daemon = new();
            ITransactionServiceHandle transactionHandle = scope.ServiceProvider.GetRequiredService<ITransactionServiceHandle>();
            RuleMetadataRepository metadataRepository = new(transactionHandle);
            RuleGroupDataAccess groups = new(transactionHandle);
            RuleTagDataAccess tags = new(transactionHandle);
            RuleMetadataService metadata = new(daemon, metadataRepository, new RuleMetadataValuesNormalizer(), scope.ServiceProvider.GetRequiredService<ILogger<RuleMetadataService>>());
            RuleInventoryService inventory = new(daemon, metadataRepository, TimeProvider.System);
            RuleMetadataReconciliationService reconciliation = new(daemon, metadataRepository);
            return new TestHost(connection, serviceProvider, scope, daemon, context, metadata, inventory, reconciliation, groups, tags);
        }

        public void SetRules(params string[] ruleIds)
        {
            ListedFirewallRule[] rules = [.. ruleIds.Select(static (ruleId, index) => new ListedFirewallRule
            {
                RuleId = ruleId,
                DisplayNumber = index + 1,
                Parsed = true,
                RawLine = ruleId,
                Rule = new FirewallRuleSpecification { AddressFamily = FirewallAddressFamily.IPv4 },
            })];
            _daemon.Response = new RuleListResponse(true, rules, TestFirewallConfiguration.Enabled);
        }


        public async Task<RuleGroupItem> CreateGroupAsync(string name, string? comment, CancellationToken cancellationToken)
        {
            string? normalizedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
            DataMutationResult result = await Groups.CreateAsync(name.Trim(), normalizedComment, cancellationToken);
            Assert.IsTrue(result.IsSuccess);
            IReadOnlyList<RuleGroupItem> groups = await Groups.GetAsync(cancellationToken);
            return groups.Single(group => string.Equals(group.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public async Task<RuleTagItem> CreateTagAsync(string name, string color, CancellationToken cancellationToken)
        {
            DataMutationResult result = await Tags.CreateAsync(name.Trim(), color.Trim().ToUpperInvariant(), cancellationToken);
            Assert.IsTrue(result.IsSuccess);
            IReadOnlyList<RuleTagItem> tags = await Tags.GetAsync(cancellationToken);
            return tags.Single(tag => string.Equals(tag.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public Task<int> ExecuteSqlAsync(string sql, CancellationToken cancellationToken) =>
            _context.Database.ExecuteSqlRawAsync(sql, cancellationToken);

        public async Task<int> MetadataRowCountAsync(CancellationToken cancellationToken)
        {
            _context.ChangeTracker.Clear();
            return await _context.Set<RuleMetadataEntry>().CountAsync(cancellationToken);
        }

        public async Task<int> MetadataTagRowCountAsync(CancellationToken cancellationToken)
        {
            _context.ChangeTracker.Clear();
            return await _context.Set<RuleMetadataTagEntry>().CountAsync(cancellationToken);
        }


        public async Task<int> RuleGroupRowCountAsync(CancellationToken cancellationToken)
        {
            _context.ChangeTracker.Clear();
            return await _context.Set<RuleGroupEntry>().CountAsync(cancellationToken);
        }

        public async Task<int> RuleTagRowCountAsync(CancellationToken cancellationToken)
        {
            _context.ChangeTracker.Clear();
            return await _context.Set<RuleTagEntry>().CountAsync(cancellationToken);
        }

        private sealed class TestDaemonRuleSource : IDaemonRuleSource
        {
            public RuleListResponse Response { get; set; } = new(true, [], TestFirewallConfiguration.Enabled);

            public Task<RuleListResponse> GetAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(Response);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _services.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
