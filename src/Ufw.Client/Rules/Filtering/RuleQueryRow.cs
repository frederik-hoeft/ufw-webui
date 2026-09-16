namespace Ufw.Client.Rules.Filtering;

internal sealed record RuleQueryRow(RuleRowProjection Row, IReadOnlyList<RuleMatchEvidence> Evidence);
