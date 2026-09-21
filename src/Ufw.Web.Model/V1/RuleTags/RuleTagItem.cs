namespace Ufw.Web.Model.V1.RuleTags;

public sealed class RuleTagItem
{
    public RuleTagItem() { }

    public RuleTagItem(Guid id, string name, string color) => (Id, Name, Color) = (id, name, color);

    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Color { get; init; } = string.Empty;
}
