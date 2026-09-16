using Microsoft.AspNetCore.Components;
using Ufw.Client.Rules.Metadata;

namespace Ufw.Client.Components.Rules.Metadata;

public sealed partial class RuleTagList
{
    [Parameter]
    public IReadOnlyList<RuleTag> Tags { get; set; } = [];

    [Parameter]
    public int MaxVisible { get; set; } = int.MaxValue;

    private IEnumerable<RuleTag> VisibleTags => Tags.Take(Math.Max(0, MaxVisible));

    private int HiddenCount => Math.Max(0, Tags.Count - Math.Max(0, MaxVisible));
}
