namespace Ufw.Web.Data.Access.Rules.Groups;

public enum RuleGroupMutationOutcome
{
    Success,
    NotFound,
    NameConflict,
    InUse,
}
