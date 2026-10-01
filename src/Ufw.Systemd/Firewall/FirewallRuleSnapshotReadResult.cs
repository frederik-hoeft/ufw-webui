using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Firewall;

internal abstract record FirewallRuleSnapshotReadResult
{
    private FirewallRuleSnapshotReadResult()
    {
    }

    internal sealed record Success : FirewallRuleSnapshotReadResult
    {
        public Success(RuleListResponse snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            Snapshot = snapshot;
        }

        public RuleListResponse Snapshot { get; }
    }

    internal sealed record Failure : FirewallRuleSnapshotReadResult
    {
        public Failure(IResponsePayload error)
        {
            ArgumentNullException.ThrowIfNull(error);
            Error = error;
        }

        public IResponsePayload Error { get; }
    }
}
