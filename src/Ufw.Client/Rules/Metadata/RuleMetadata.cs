namespace Ufw.Client.Rules.Metadata;

public sealed record RuleMetadata(string? Group, string? Notes, IReadOnlyList<string> Tags);
