namespace Ufw.Web.Data.Access.Rules;

internal sealed record RuleTagsNotFoundError(IReadOnlyList<Guid> TagIds) : DataMutationError;
