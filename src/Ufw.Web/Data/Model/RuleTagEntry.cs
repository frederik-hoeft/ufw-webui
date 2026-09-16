namespace Ufw.Web.Data.Model;

internal sealed partial class RuleTagEntry
{
    public const int MAX_NAME_LENGTH = 64;
    public const int COLOR_LENGTH = 7;

    public long Id { get; set; }

    public Guid PublicId { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public required string Color { get; set; }

    public ICollection<RuleMetadataTagEntry> RuleMetadata { get; set; } = [];
}
