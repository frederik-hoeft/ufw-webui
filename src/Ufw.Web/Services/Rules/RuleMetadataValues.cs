namespace Ufw.Web.Services.Rules;

internal sealed record RuleMetadataValues(string? Notes, IReadOnlyList<Guid> TagIds, Guid? GroupId)
{
    public bool IsEmpty => Notes is null && TagIds.Count == 0 && GroupId is null;
}
