namespace Ufw.Web.Client.Features.Rules.Insertion;

internal enum OrderedRuleInsertionContextError
{
    None,
    Incomplete,
    InvalidFingerprint,
    InvalidPlacement,
    StaleBaseline,
    AnchorUnavailable,
    CapabilityUnavailable,
}
