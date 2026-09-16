namespace Ufw.Web.Services.Rules;

internal sealed record RuleMetadataValues(string? Notes, IReadOnlyList<Guid> TagIds)
{
    public bool IsEmpty => Notes is null && TagIds.Count == 0;
}
