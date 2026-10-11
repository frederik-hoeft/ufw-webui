using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Firewall;

namespace Ufw.Systemd.Tests.TestSupport;

internal static class FirewallRuleSnapshotReadResultTestExtensions
{
    public static FirewallSnapshot GetRequiredSnapshot(this FirewallRuleSnapshotReadResult result) =>
        Assert.IsInstanceOfType<FirewallRuleSnapshotReadResult.Success>(result).Snapshot;

    public static string ComputeSnapshotFingerprint(this FirewallRuleSnapshotReadResult result) =>
        FirewallRuleSnapshotFingerprint.Compute(result.GetRequiredSnapshot().Active, result.GetRequiredSnapshot().Rules);
}
