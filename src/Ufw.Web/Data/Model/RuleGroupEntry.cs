namespace Ufw.Web.Data.Model;

internal sealed partial class RuleGroupEntry
{
    public long Id { get; set; }

    public Guid PublicId { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public string? Comment { get; set; }

    public ICollection<RuleMetadataEntry> RuleMetadata { get; set; } = [];

    public ICollection<RuleTemplateEntry> RuleTemplates { get; set; } = [];
}
