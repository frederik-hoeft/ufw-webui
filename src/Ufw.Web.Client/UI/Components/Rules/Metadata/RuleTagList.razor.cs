using Microsoft.AspNetCore.Components;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.UI.Components.Rules.Metadata;

public sealed partial class RuleTagList
{
    [Parameter]
    public IReadOnlyList<RuleTag> Tags { get; set; } = [];

    [Parameter]
    public int MaxVisible { get; set; } = int.MaxValue;

    private IEnumerable<RuleTag> VisibleTags => Tags.Take(Math.Max(0, MaxVisible));

    private int HiddenCount => Math.Max(0, Tags.Count - Math.Max(0, MaxVisible));
}
