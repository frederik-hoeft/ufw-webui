namespace Ufw.Web.Services.Rules;

public enum RuleGroupMutationOutcome
{
    Success,
    NotFound,
    NameConflict,
    InUse,
    InvalidGroup,
}
