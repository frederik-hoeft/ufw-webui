using Microsoft.AspNetCore.Components;
using Ufw.Client.Rules.Metadata;

namespace Ufw.Client.Components.Rules.Metadata;

public sealed partial class RuleTagChip
{
    [Parameter, EditorRequired]
    public RuleTag Tag { get; set; } = null!;

    [Parameter]
    public EventCallback OnRemove { get; set; }

    [Parameter]
    public bool Disabled { get; set; }
}
