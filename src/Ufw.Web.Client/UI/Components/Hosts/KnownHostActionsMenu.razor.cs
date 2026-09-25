using Microsoft.AspNetCore.Components;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.UI.Components.Hosts;

public sealed partial class KnownHostActionsMenu
{
    [Parameter, EditorRequired]
    public KnownHostInventoryItem Host { get; set; } = null!;

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public EventCallback<KnownHostInventoryItem> EditRequested { get; set; }

    [Parameter]
    public EventCallback<KnownHostInventoryItem> ReconcileDnsRequested { get; set; }

    [Parameter]
    public EventCallback<KnownHostInventoryItem> DeleteRequested { get; set; }
}
