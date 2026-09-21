using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.UI.Components.Hosts;

public sealed partial class DeleteKnownHostDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter, EditorRequired]
    public KnownHostInventoryItem Host { get; set; } = null!;

    private void Cancel() => MudDialog.Cancel();

    private void Confirm() => MudDialog.Close(DialogResult.Ok(true));
}
