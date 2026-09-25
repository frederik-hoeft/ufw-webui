using Microsoft.AspNetCore.Components;
using System.Linq.Expressions;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Model.V1.KnownHosts;
using Ufw.Web.Client.Features.KnownHosts;

namespace Ufw.Web.Client.UI.Components.Rules;

public sealed partial class KnownHostField
{
    private KnownHostFieldState _state = null!;

    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public string? Placeholder { get; set; }

    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    [Parameter]
    public Expression<Func<string?>>? For { get; set; }

    [Parameter]
    public IReadOnlyList<KnownHostInventoryItem> Suggestions { get; set; } = [];

    [Parameter]
    public FirewallAddressFamily AddressFamily { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    private Task<IEnumerable<string>> SearchAsync(string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<KnownHostInventoryItem> matches = HostSuggestions.Search(Suggestions, AddressFamily, value);
        return Task.FromResult(matches.Select(HostSuggestions.GetSelectionValue));
    }

    protected override void OnInitialized() => _state = new KnownHostFieldState(HostSuggestions);

    protected override void OnParametersSet() => _state.Synchronize(Value, Suggestions);

    private Task ValueChangedAsync(string? value) =>
        ValueChanged.InvokeAsync(_state.ApplyInput(value, Suggestions, AddressFamily));

    private KnownHostInventoryItem? FindSuggestion(string? selectionValue) =>
        HostSuggestions.ResolveSelectionValue(Suggestions, AddressFamily, selectionValue);
}
