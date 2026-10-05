namespace Ufw.Web.Data.Access.Rules.Metadata;

internal enum RuleMetadataReplacementPersistenceOutcome
{
    Unchanged,
    ClearedStaleTarget,
    Copied,
    Rekeyed,
}
