using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Client.Features.Rules.Metadata;

/// <summary>
/// Captures the rule occurrences and group membership confirmed for a single batch deletion.
/// The occurrence IDs are local to <see cref="Baseline"/>.
/// </summary>
internal sealed record RuleGroupDeletionPlan(
    Guid GroupId,
    RuleListResponse Baseline,
    IReadOnlyList<int> OccurrenceIds,
    IReadOnlyList<string> ExpectedRuleIds,
    IReadOnlyList<Guid> ExpectedTemplateIds);
