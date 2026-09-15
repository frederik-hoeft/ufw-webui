namespace Ufw.Client.Rules;

internal sealed record RuleTableProjection(IReadOnlyList<RuleFamilyProjection> Families)
{
    public static RuleTableProjection Empty { get; } = new([]);
}
