using Microsoft.AspNetCore.Components;
using System.Linq.Expressions;
using Ufw.Client.Api;

namespace Ufw.Client.Components.Rules;

public sealed partial class NetworkInterfaceField
{
    private readonly string _inputId = $"network-interface-{Guid.NewGuid():N}";

    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public string? HelperText { get; set; }

    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    [Parameter]
    public Expression<Func<string?>>? For { get; set; }

    [Parameter]
    public IReadOnlyList<NetworkInterfaceInventoryItem> Suggestions { get; set; } = [];

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public string? Class { get; set; }

    private string CssClass => string.IsNullOrWhiteSpace(Class)
        ? "network-interface-field"
        : $"network-interface-field {Class}";

    private Task<IEnumerable<string>> SearchAsync(string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<NetworkInterfaceInventoryItem> matches = string.IsNullOrWhiteSpace(value)
            ? Suggestions
            : Suggestions.Where(candidate =>
                candidate.Name.Contains(value, StringComparison.OrdinalIgnoreCase)
                || candidate.Comment?.Contains(value, StringComparison.OrdinalIgnoreCase) == true);

        return Task.FromResult(matches.Select(static candidate => candidate.Name));
    }

    private NetworkInterfaceInventoryItem? FindSuggestion(string name) => Suggestions.FirstOrDefault(
        candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));
}
