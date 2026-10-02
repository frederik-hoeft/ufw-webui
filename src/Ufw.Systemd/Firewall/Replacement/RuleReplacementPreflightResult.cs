using Ufw.Shared.Firewall;

namespace Ufw.Systemd.Firewall.Replacement;

internal abstract record RuleReplacementPreflightResult
{
    private RuleReplacementPreflightResult()
    {
    }

    internal sealed record Ready : RuleReplacementPreflightResult
    {
        public Ready(RuleReplacementPreflight preflight)
        {
            ArgumentNullException.ThrowIfNull(preflight);
            Preflight = preflight;
        }

        public RuleReplacementPreflight Preflight { get; }
    }

    internal sealed record NoChange : RuleReplacementPreflightResult
    {
        public NoChange(ListedFirewallRule target)
        {
            ArgumentNullException.ThrowIfNull(target);
            Target = target;
        }

        public ListedFirewallRule Target { get; }
    }

    internal sealed record Rejected : RuleReplacementPreflightResult
    {
        public Rejected(RuleReplacementExecutionOutcome outcome, string diagnostic)
        {
            if (outcome is not (RuleReplacementExecutionOutcome.PreconditionFailed or RuleReplacementExecutionOutcome.StateUncertain))
            {
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Replacement preflight rejection must describe a precondition failure or uncertain state.");
            }
            ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);
            Outcome = outcome;
            Diagnostic = diagnostic;
        }

        public RuleReplacementExecutionOutcome Outcome { get; }

        public string Diagnostic { get; }
    }
}
