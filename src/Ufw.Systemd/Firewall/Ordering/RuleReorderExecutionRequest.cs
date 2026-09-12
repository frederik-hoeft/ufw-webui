namespace Ufw.Systemd.Firewall.Ordering;

internal sealed record RuleReorderExecutionRequest(string BaselineFingerprint, IReadOnlyList<int> DesiredOrder);
