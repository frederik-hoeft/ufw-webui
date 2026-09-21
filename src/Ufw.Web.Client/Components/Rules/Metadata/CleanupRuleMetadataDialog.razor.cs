using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Ufw.Web.Client.Components.Rules.Metadata;

public sealed partial class CleanupRuleMetadataDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public int Count { get; set; }

    private void Cancel() => MudDialog.Cancel();

    private void Confirm() => MudDialog.Close(DialogResult.Ok(true));
}
