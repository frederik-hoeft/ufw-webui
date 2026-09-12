namespace Ufw.Client.RuleInsertion;

internal enum OrderedRuleInsertionContextError
{
    None,
    Incomplete,
    InvalidFingerprint,
    InvalidPlacement,
    StaleBaseline,
    AnchorUnavailable,
}
