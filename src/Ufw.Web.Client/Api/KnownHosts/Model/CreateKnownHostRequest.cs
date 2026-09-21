namespace Ufw.Web.Client.Api.KnownHosts.Model;

public sealed class CreateKnownHostRequest
{
    public string Name { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public string? Comment { get; init; }

    public bool IsVisible { get; init; } = true;
}
