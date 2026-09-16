namespace Ufw.Web.Data.Model;

internal sealed partial class RuleMetadataEntry
{
    public const int MAX_RULE_ID_LENGTH = 128;
    public const int MAX_GROUP_LENGTH = 128;
    public const int MAX_NOTES_LENGTH = 4000;

    public long Id { get; set; }

    public required string RuleId { get; set; }

    public string? Group { get; set; }

    public string? Notes { get; set; }

    public ICollection<RuleMetadataTagEntry> Tags { get; set; } = [];
}
