namespace Ufw.Web.Client.Features.Rules.Metadata;

public sealed record RuleMetadata(Guid Id, string? Notes, IReadOnlyList<RuleTag> Tags);
