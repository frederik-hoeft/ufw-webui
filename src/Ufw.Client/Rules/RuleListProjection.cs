namespace Ufw.Client.Rules;

internal sealed record RuleListProjection(IReadOnlyList<RuleFamilyProjection> Families)
{
    public static RuleListProjection Empty { get; } = new([]);
}
