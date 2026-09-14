using Ufw.Client.Api;
using Ufw.Shared.Firewall;

namespace Ufw.Client.KnownHosts;

internal sealed class KnownHostFieldState
{
    private Guid? _selectedHostId;

    public string? DisplayValue { get; private set; }

    public void Synchronize(string? value, IReadOnlyList<KnownHostInventoryItem> suggestions)
    {
        ArgumentNullException.ThrowIfNull(suggestions);

        KnownHostInventoryItem? selectedHost = FindSelectedHost(suggestions);
        if (selectedHost is not null && string.Equals(selectedHost.Address, value, StringComparison.Ordinal))
        {
            DisplayValue = KnownHostSuggestions.GetSelectionValue(selectedHost);
            return;
        }

        _selectedHostId = null;
        DisplayValue = value;
    }

    public string? ApplyInput(
        string? value,
        IReadOnlyList<KnownHostInventoryItem> suggestions,
        FirewallAddressFamily addressFamily)
    {
        ArgumentNullException.ThrowIfNull(suggestions);

        KnownHostInventoryItem? selectedHost = KnownHostSuggestions.ResolveSelectionValue(suggestions, addressFamily, value);
        if (selectedHost is null)
        {
            _selectedHostId = null;
            DisplayValue = value;
            return value;
        }

        _selectedHostId = selectedHost.Id;
        DisplayValue = KnownHostSuggestions.GetSelectionValue(selectedHost);
        return selectedHost.Address;
    }

    private KnownHostInventoryItem? FindSelectedHost(IReadOnlyList<KnownHostInventoryItem> suggestions)
    {
        if (_selectedHostId is not Guid selectedHostId)
        {
            return null;
        }

        return suggestions.FirstOrDefault(host => host.Id == selectedHostId);
    }
}
