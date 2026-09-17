namespace Ufw.Web.Api.V1.Models.Rules;

public sealed record RuleMetadataItem(Guid Id, string RuleId, string? Notes, IReadOnlyList<RuleTagItem> Tags);
