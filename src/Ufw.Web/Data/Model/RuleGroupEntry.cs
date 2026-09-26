namespace Ufw.Web.Data.Model;

internal sealed partial class RuleGroupEntry
{
    public const int MAX_NAME_LENGTH = 64;
    public const int MAX_COMMENT_LENGTH = 4000;

    public long Id { get; set; }

    public Guid PublicId { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public string? Comment { get; set; }

    public ICollection<RuleMetadataEntry> RuleMetadata { get; set; } = [];
}
