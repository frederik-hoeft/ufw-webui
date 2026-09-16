namespace Ufw.Web.Data.Model;

internal sealed partial class RuleMetadataTagEntry
{
    public const int MAX_NAME_LENGTH = 64;

    public long Id { get; set; }

    public long RuleMetadataId { get; set; }

    public required RuleMetadataEntry RuleMetadata { get; set; }

    public required string Name { get; set; }

    public required string NormalizedName { get; set; }
}
