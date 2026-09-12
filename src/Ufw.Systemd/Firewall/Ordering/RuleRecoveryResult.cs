using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Firewall.Ordering;

internal sealed record RuleRecoveryResult(
    bool PresenceConfirmed,
    bool InsertionAttempted,
    RuleListResponse? Snapshot,
    string? Diagnostic);
