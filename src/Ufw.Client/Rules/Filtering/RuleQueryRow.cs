namespace Ufw.Client.Rules.Filtering;

public sealed record RuleQueryRow(RuleRowProjection Row, IReadOnlyList<RuleMatchEvidence> Evidence);
