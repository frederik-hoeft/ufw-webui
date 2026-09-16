namespace Ufw.Web.Api.V1.Models.Rules;

public sealed record RuleMetadataItem(string RuleId, string? Group, string? Notes, IReadOnlyList<string> Tags);
