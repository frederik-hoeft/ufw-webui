namespace Ufw.Systemd.Firewall.Ordering;

internal abstract record RuleReorderPreflightResult
{
    private RuleReorderPreflightResult()
    {
    }

    internal sealed record Accepted : RuleReorderPreflightResult
    {
        public Accepted(RuleReorderPreflight preflight)
        {
            ArgumentNullException.ThrowIfNull(preflight);
            Preflight = preflight;
        }

        public RuleReorderPreflight Preflight { get; }
    }

    internal sealed record Rejected : RuleReorderPreflightResult
    {
        public Rejected(string diagnostic)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);
            Diagnostic = diagnostic;
        }

        public string Diagnostic { get; }
    }
}
