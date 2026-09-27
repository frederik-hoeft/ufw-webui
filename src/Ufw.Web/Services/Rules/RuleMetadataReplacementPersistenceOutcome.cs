namespace Ufw.Web.Services.Rules;

internal enum RuleMetadataReplacementPersistenceOutcome
{
    Unchanged,
    ClearedStaleTarget,
    Copied,
    Rekeyed,
}
