using Ufw.Client.Api;
using Ufw.Shared.Firewall;

namespace Ufw.Client.KnownHosts;

internal static class KnownHostSuggestions
{
    public static IEnumerable<KnownHostInventoryItem> Search(
        IReadOnlyList<KnownHostInventoryItem> suggestions,
        FirewallAddressFamily addressFamily,
        string? query)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        IEnumerable<KnownHostInventoryItem> compatible = suggestions.Where(host => IsCompatible(host, addressFamily));
        if (string.IsNullOrWhiteSpace(query))
        {
            return compatible;
        }

        return compatible.Where(host =>
            host.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || host.Address.Contains(query, StringComparison.OrdinalIgnoreCase)
            || host.Comment?.Contains(query, StringComparison.OrdinalIgnoreCase) == true);
    }

    public static FirewallAddressFamily ResolveCompatibleAddressFamily(FirewallAddressFamily declaredFamily, string? oppositeAddress) =>
        declaredFamily != FirewallAddressFamily.Any
            ? declaredFamily
            : RuleSpecificationNormalizer.GetAddressFamily(oppositeAddress);

    public static string GetSelectionValue(KnownHostInventoryItem host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return $"{host.Name} [{host.Address}]";
    }

    public static KnownHostInventoryItem? ResolveSelectionValue(
        IReadOnlyList<KnownHostInventoryItem> suggestions,
        FirewallAddressFamily addressFamily,
        string? selectionValue)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        if (string.IsNullOrWhiteSpace(selectionValue))
        {
            return null;
        }

        return suggestions.FirstOrDefault(host =>
            IsCompatible(host, addressFamily)
            && string.Equals(GetSelectionValue(host), selectionValue, StringComparison.Ordinal));
    }

    private static bool IsCompatible(KnownHostInventoryItem host, FirewallAddressFamily addressFamily) =>
        addressFamily == FirewallAddressFamily.Any || host.AddressFamily == addressFamily;
}
