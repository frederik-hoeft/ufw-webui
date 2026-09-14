namespace Ufw.Client.Api;

public sealed class UpdateKnownHostRequest
{
    public string Name { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public string? Comment { get; init; }

    public bool IsVisible { get; init; } = true;
}
