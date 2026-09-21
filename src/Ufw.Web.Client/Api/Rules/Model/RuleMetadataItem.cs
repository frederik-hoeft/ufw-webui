using Ufw.Web.Client.Api.RuleTags.Model;
namespace Ufw.Web.Client.Api.Rules.Model;

public sealed class RuleMetadataItem
{
    public Guid Id { get; init; }

    public string RuleId { get; init; } = string.Empty;

    public string? Notes { get; init; }

    public IReadOnlyList<RuleTagItem> Tags { get; init; } = [];
}
