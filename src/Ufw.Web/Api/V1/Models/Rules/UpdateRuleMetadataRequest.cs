namespace Ufw.Web.Api.V1.Models.Rules;

public sealed class UpdateRuleMetadataRequest
{
    public string? Group { get; init; }

    public string? Notes { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
}
