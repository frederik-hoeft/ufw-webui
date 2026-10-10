using Microsoft.AspNetCore.Components;

namespace Ufw.Web.Client.UI.Components;

/// <summary>Presentation shared by simple destructive confirmations; callers own their decision and mutation semantics.</summary>
public sealed partial class ConfirmationDialog
{
    [Parameter, EditorRequired]
    public required string Prompt { get; set; }

    [Parameter, EditorRequired]
    public required string Description { get; set; }

    [Parameter, EditorRequired]
    public required string ConfirmLabel { get; set; }

    [Parameter, EditorRequired]
    public EventCallback OnCancel { get; set; }

    [Parameter, EditorRequired]
    public EventCallback OnConfirm { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public bool Busy { get; set; }

    [Parameter]
    public bool ConfirmDisabled { get; set; }
}
