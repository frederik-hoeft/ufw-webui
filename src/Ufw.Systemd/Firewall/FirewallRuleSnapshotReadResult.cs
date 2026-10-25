namespace Ufw.Systemd.Firewall;

internal abstract record FirewallRuleSnapshotReadResult
{
    private FirewallRuleSnapshotReadResult()
    {
    }

    internal sealed record Success : FirewallRuleSnapshotReadResult
    {
        public Success(FirewallSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            Snapshot = snapshot;
        }

        public FirewallSnapshot Snapshot { get; }
    }

    internal sealed record Failure : FirewallRuleSnapshotReadResult
    {
        public Failure(string diagnostic)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);
            Diagnostic = diagnostic;
        }

        public string Diagnostic { get; }
    }
}
