using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Web.Client.Features.KnownHosts.Api;

namespace Ufw.Web.Client.Components.Hosts;

public sealed partial class DeleteKnownHostDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter, EditorRequired]
    public KnownHostInventoryItem Host { get; set; } = null!;

    private void Cancel() => MudDialog.Cancel();

    private void Confirm() => MudDialog.Close(DialogResult.Ok(true));
}
