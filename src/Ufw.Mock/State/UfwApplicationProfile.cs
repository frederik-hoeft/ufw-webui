namespace Ufw.Mock.State;

internal sealed class UfwApplicationProfile
{
    public required string Name { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public required string Ports { get; set; }
}
