namespace Ufw.Web.Services.Rules;

internal sealed record RuleMetadataValues(string? Group, string? Notes, IReadOnlyList<RuleMetadataTagValues> Tags)
{
    public bool IsEmpty => Group is null && Notes is null && Tags.Count == 0;
}
