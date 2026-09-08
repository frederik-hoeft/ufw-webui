namespace Ufw.Web.Data.Model;

internal sealed partial class NetworkInterfaceEntry
{
    public const int MAX_NAME_LENGTH = 256;
    public const int MAX_COMMENT_LENGTH = 200;

    public long Id { get; set; }

    public Guid PublicId { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public string? Comment { get; set; }
}
