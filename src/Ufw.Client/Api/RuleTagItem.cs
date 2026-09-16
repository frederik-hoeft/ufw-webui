namespace Ufw.Client.Api;

public sealed class RuleTagItem
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Color { get; init; } = string.Empty;
}
