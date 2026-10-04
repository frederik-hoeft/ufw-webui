namespace Ufw.Web.Data.Access.Rules;

internal sealed record RuleGroupNotFoundError(Guid GroupId) : DataMutationError;
