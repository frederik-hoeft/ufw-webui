namespace Ufw.Web.Model.V1.Rules;

public sealed class UpdateRuleMetadataRequest
{
    public string? Notes { get; init; }

    public IReadOnlyList<Guid> TagIds { get; init; } = [];

    public Guid? GroupId { get; init; }
}
