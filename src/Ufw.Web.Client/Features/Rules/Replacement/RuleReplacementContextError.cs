namespace Ufw.Web.Client.Features.Rules.Replacement;

internal enum RuleReplacementContextError
{
    None,
    Incomplete,
    InvalidFingerprint,
    StaleBaseline,
    TargetUnavailable,
    TargetMismatch,
    DuplicateIdentity,
    CapabilityUnavailable,
}
