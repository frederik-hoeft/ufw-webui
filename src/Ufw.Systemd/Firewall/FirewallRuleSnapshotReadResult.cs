using Ufw.Shared.Ipc.Model;
using Ufw.Systemd.Interop.Output;

namespace Ufw.Systemd.Firewall;

internal readonly record struct FirewallRuleSnapshotReadResult(IResponsePayload? Error, UfwStatusSnapshot? Snapshot);
