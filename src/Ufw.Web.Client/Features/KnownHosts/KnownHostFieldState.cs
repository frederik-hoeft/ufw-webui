using Ufw.Shared.Firewall;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Client.Api.KnownHosts.Model;

namespace Ufw.Web.Client.Features.KnownHosts;

internal sealed class KnownHostFieldState(IKnownHostSuggestionService suggestions)
{
    private Guid? _selectedHostId;

    public string? DisplayValue { get; private set; }

    public void Synchronize(string? value, IReadOnlyList<KnownHostInventoryItem> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        KnownHostInventoryItem? selectedHost = FindSelectedHost(candidates);
        if (selectedHost is not null && string.Equals(selectedHost.Address, value, StringComparison.Ordinal))
        {
            DisplayValue = suggestions.GetSelectionValue(selectedHost);
            return;
        }

        _selectedHostId = null;
        DisplayValue = value;
    }

    public string? ApplyInput(string? value, IReadOnlyList<KnownHostInventoryItem> candidates, FirewallAddressFamily addressFamily)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        KnownHostInventoryItem? selectedHost = suggestions.ResolveSelectionValue(candidates, addressFamily, value);
        if (selectedHost is null)
        {
            _selectedHostId = null;
            DisplayValue = value;
            return value;
        }

        _selectedHostId = selectedHost.Id;
        DisplayValue = suggestions.GetSelectionValue(selectedHost);
        return selectedHost.Address;
    }

    private KnownHostInventoryItem? FindSelectedHost(IReadOnlyList<KnownHostInventoryItem> candidates)
    {
        if (_selectedHostId is not Guid selectedHostId)
        {
            return null;
        }

        return candidates.FirstOrDefault(host => host.Id == selectedHostId);
    }
}
