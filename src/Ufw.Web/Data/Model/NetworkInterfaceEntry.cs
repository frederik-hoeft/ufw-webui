namespace Ufw.Web.Data.Model;

internal sealed partial class NetworkInterfaceEntry
{
    public long Id { get; set; }

    public Guid PublicId { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public string? Comment { get; set; }

    public bool IsVisible { get; set; } = true;

    public bool IsPresent { get; set; } = true;
}
