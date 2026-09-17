namespace Ufw.Web.Api.V1.Models.Rules;

public sealed class UpdateRuleMetadataRequest
{
    public string? Notes { get; init; }

    public IReadOnlyList<Guid> TagIds { get; init; } = [];
}
