namespace Ufw.Web.Services.Rules;

public enum RuleTemplateMutationOutcome
{
    Success,
    NotFound,
    NameConflict,
    InvalidTemplate,
    TagNotFound,
    GroupNotFound,
}
