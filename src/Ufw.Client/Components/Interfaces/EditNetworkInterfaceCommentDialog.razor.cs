using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Ufw.Client.Components.Interfaces;

public sealed partial class EditNetworkInterfaceCommentDialog
{
    private string? _comment;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter, EditorRequired]
    public string InterfaceName { get; set; } = string.Empty;

    [Parameter]
    public string? Comment { get; set; }

    protected override void OnParametersSet() => _comment ??= Comment;

    private void Cancel() => MudDialog.Cancel();

    private void Save() => MudDialog.Close(DialogResult.Ok(_comment ?? string.Empty));
}
