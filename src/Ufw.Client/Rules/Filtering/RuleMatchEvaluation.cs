namespace Ufw.Client.Rules.Filtering;

internal sealed record RuleMatchEvaluation(bool Matches, IReadOnlyList<RuleMatchEvidence> Evidence)
{
    public static RuleMatchEvaluation NoMatch { get; } = new(false, []);

    public static RuleMatchEvaluation Match(params RuleMatchEvidence[] evidence) => new(true, evidence);
}
