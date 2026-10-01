namespace Ufw.Web.Data.Model;

internal sealed partial class RuleTemplateTagEntry
{
    public long Id { get; set; }

    public long RuleTemplateId { get; set; }

    public required RuleTemplateEntry RuleTemplate { get; set; }

    public long TagId { get; set; }

    public required RuleTagEntry Tag { get; set; }
}
