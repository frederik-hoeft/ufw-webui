namespace Ufw.Web.Data.Access.Rules;

public sealed record RuleGroupNotFoundError(Guid GroupId) : DataMutationError;
