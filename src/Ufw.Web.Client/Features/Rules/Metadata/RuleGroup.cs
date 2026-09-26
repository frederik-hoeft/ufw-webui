namespace Ufw.Web.Client.Features.Rules.Metadata;

public sealed record RuleGroup(Guid Id, string Name, string? Comment, IReadOnlyList<string> RuleIds);
