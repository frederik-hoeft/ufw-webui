using Microsoft.AspNetCore.Components;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Components.Rules.Metadata;

public sealed partial class RuleTagChip
{
    [Parameter, EditorRequired]
    public RuleTag Tag { get; set; } = null!;

    [Parameter]
    public EventCallback OnRemove { get; set; }

    [Parameter]
    public bool Disabled { get; set; }
}
