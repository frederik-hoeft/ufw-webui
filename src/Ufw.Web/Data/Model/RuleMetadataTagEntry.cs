namespace Ufw.Web.Data.Model;

internal sealed partial class RuleMetadataTagEntry
{
    public long Id { get; set; }

    public long RuleMetadataId { get; set; }

    public required RuleMetadataEntry RuleMetadata { get; set; }

    public long TagId { get; set; }

    public required RuleTagEntry Tag { get; set; }
}
