using Microsoft.AspNetCore.Components;
using System.Linq.Expressions;
using Ufw.Client.Api;
using Ufw.Client.KnownHosts;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Components.Rules;

public sealed partial class KnownHostField
{
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

        IEnumerable<KnownHostInventoryItem> matches = KnownHostSuggestions.Search(Suggestions, AddressFamily, value);
        return Task.FromResult(matches.Select(KnownHostSuggestions.GetSelectionValue));
    }

    private Task ValueChangedAsync(string? value) =>
        ValueChanged.InvokeAsync(KnownHostSuggestions.ResolveInputValue(Suggestions, AddressFamily, value));

    private KnownHostInventoryItem? FindSuggestion(string? selectionValue) =>
        KnownHostSuggestions.ResolveSelectionValue(Suggestions, AddressFamily, selectionValue);
}
