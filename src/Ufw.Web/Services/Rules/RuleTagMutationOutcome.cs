namespace Ufw.Web.Services.Rules;

public enum RuleTagMutationOutcome
{
    Success,
    NotFound,
    NameConflict,
    InUse,
    InvalidTag,
}
