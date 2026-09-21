namespace Ufw.Web.Client.Api.Rules.Model;

public sealed class UpdateRuleMetadataRequest
{
    public string? Notes { get; init; }

    public IReadOnlyList<Guid> TagIds { get; init; } = [];
}
