namespace Ufw.Web.Data.Access.Rules;

public sealed record RuleTagsNotFoundError(IReadOnlyList<Guid> TagIds) : DataMutationError;
