using Ufw.Web.Data.Model;

namespace Ufw.Web.Data.Access.Rules;

internal sealed record RuleManagementDependencies(DataMutationError? Error, IReadOnlyList<RuleTagEntry> Tags, RuleGroupEntry? Group);
