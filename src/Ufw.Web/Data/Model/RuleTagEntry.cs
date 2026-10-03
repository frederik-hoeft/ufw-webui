namespace Ufw.Web.Data.Model;

internal sealed partial class RuleTagEntry
{
    public long Id { get; set; }

    public Guid PublicId { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public required string Color { get; set; }

    public ICollection<RuleMetadataTagEntry> RuleMetadata { get; set; } = [];

    public ICollection<RuleTemplateTagEntry> RuleTemplates { get; set; } = [];
}
