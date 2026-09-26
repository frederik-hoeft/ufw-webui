namespace Ufw.Web.Data.Model;

internal sealed partial class RuleMetadataEntry
{
    public const int MAX_RULE_ID_LENGTH = 128;
    public const int MAX_NOTES_LENGTH = 4000;

    public long Id { get; set; }

    public Guid PublicId { get; set; } = Guid.CreateVersion7();

    public required string RuleId { get; set; }

    public string? Notes { get; set; }

    public long? GroupId { get; set; }

    public RuleGroupEntry? Group { get; set; }

    public ICollection<RuleMetadataTagEntry> Tags { get; set; } = [];
}
