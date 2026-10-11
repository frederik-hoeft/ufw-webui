using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Firewall;

/// <summary>Maps daemon firewall snapshots to the stable IPC response representation.</summary>
internal static class FirewallSnapshotResponseMapper
{
    public static RuleListResponse ToResponse(this FirewallSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new RuleListResponse(snapshot.Active, snapshot.Rules, snapshot.Configuration) { Assessment = snapshot.Assessment };
    }
}
