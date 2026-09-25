namespace Ufw.Web.Client.Features.Rules.Filtering;

public sealed record RuleQuery(IReadOnlyList<RuleFilter> Filters)
{
    public static RuleQuery Empty { get; } = new([]);

    public bool IsActive => Filters.Count > 0;
}
