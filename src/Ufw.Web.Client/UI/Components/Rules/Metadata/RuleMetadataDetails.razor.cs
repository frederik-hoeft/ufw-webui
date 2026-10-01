using Microsoft.AspNetCore.Components;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.UI.Components.Rules.Metadata;

public sealed partial class RuleMetadataDetails
{
    [Parameter]
    public RuleMetadata? Metadata { get; set; }

    [Parameter]
    public string? CanonicalCommand { get; set; }

    [Parameter]
    public string? Source { get; set; }

    [Parameter]
    public string? Destination { get; set; }

    [Parameter]
    public EventCallback<KnownHostInventoryResponse> KnownHostsChanged { get; set; }

    [Parameter]
    public RenderFragment? Header { get; set; }

    [Parameter]
    public RenderFragment? Actions { get; set; }
}
