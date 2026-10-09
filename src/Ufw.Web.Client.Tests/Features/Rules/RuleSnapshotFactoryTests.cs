using Ufw.Shared.Firewall;
﻿using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Features.Rules;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.Tests.Features.Rules;

[TestClass]
public sealed class RuleSnapshotFactoryTests
{
    [TestMethod]
    public void FromInventoryResponse_PreservesAuthoritativeSnapshotAndFiltersStaleMetadata()
    {
        Guid liveMetadataId = Guid.CreateVersion7();
        RuleListResponse firewall = new(true, [new ListedFirewallRule { RuleId = "live" }], TestFirewallConfiguration.Enabled);
        RuleInventoryResponse inventory = new(firewall,
        [
            new RuleMetadataItem { Id = liveMetadataId, RuleId = "live" },
            new RuleMetadataItem { Id = Guid.CreateVersion7(), RuleId = "orphan" },
        ]);

        RuleSnapshot snapshot = RuleSnapshotFactory.FromInventoryResponse(inventory);

        Assert.IsTrue(snapshot.FirewallActive);
        Assert.HasCount(1, snapshot.Metadata);
        Assert.AreEqual(liveMetadataId, snapshot.Metadata["live"].Id);
    }

    [TestMethod]
    public void ApplyMetadataMutation_RejectsDifferentRuleAndPreservesOriginalSnapshot()
    {
        RuleListResponse firewall = new(true, [new ListedFirewallRule { RuleId = "live" }], TestFirewallConfiguration.Enabled);
        RuleSnapshot snapshot = RuleSnapshotFactory.FromFirewallResponse(firewall);
        RuleMetadataMutationResponse mismatch = new(new RuleMetadataItem { Id = Guid.CreateVersion7(), RuleId = "other" });

        Assert.ThrowsExactly<ApiProtocolException>(() => RuleSnapshotFactory.ApplyMetadataMutation(snapshot, "live", mismatch));
        Assert.IsEmpty(snapshot.Metadata);
        Assert.ThrowsExactly<InvalidOperationException>(() => RuleSnapshotFactory.ApplyMetadataMutation(snapshot, "missing", new RuleMetadataMutationResponse()));
    }
}
