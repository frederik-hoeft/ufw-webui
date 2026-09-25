using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ufw.Web.Client.Features.Rules.Presentation;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.UI.Components.Rules.Metadata;

public sealed partial class RuleKnownHostEndpoint
{
    private bool _busy;

    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public string? Endpoint { get; set; }

    [Parameter]
    public EventCallback<KnownHostInventoryResponse> KnownHostsChanged { get; set; }

    private RuleEndpointKnownHostProjection Projection => ProjectionService.Project(Endpoint, KnownHosts.Current?.Hosts ?? []);

    private string DisplayLabel => Projection.Hosts.Count > 0 ? RulesText["KnownHostEndpointLabel", Label] : Label;

    private Task CreateAsync() => RunEditorAsync(cancellationToken => KnownHostEditor.CreateAsync(Projection.Address, cancellationToken));

    private Task EditAsync(KnownHostInventoryItem host) => RunEditorAsync(cancellationToken => KnownHostEditor.EditAsync(host, cancellationToken));

    private async Task RunEditorAsync(Func<CancellationToken, Task<KnownHostInventoryResponse?>> operation)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            KnownHostInventoryResponse? response = await operation(CancellationToken.None);
            if (response is not null)
            {
                await KnownHostsChanged.InvokeAsync(response);
            }
        }
        catch (Exception exception) when (ClientErrors.TryDescribe(exception, out _))
        {
            Snackbar.Add(ClientErrors.Describe(exception).Message, Severity.Error);
        }
        finally
        {
            _busy = false;
        }
    }
}
