namespace Ufw.Web.Client.Features.Rules.Filtering;

public sealed record RuleQueryRow(RuleRowProjection Row, IReadOnlyList<RuleMatchEvidence> Evidence);
