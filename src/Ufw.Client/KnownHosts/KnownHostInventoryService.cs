using Ufw.Client.Api;
using Ufw.Shared.Firewall;

namespace Ufw.Client.KnownHosts;

internal sealed class KnownHostInventoryService(IKnownHostApiClient apiClient) : IKnownHostInventoryService
{
    public KnownHostInventoryResponse? Current { get; private set; }

    public async Task<KnownHostInventoryResponse> RefreshAsync(CancellationToken cancellationToken = default)
    {
        Current = Normalize(await apiClient.GetAsync(cancellationToken));
        return Current;
    }

    public async Task<KnownHostInventoryResponse> CreateAsync(CreateKnownHostRequest request, CancellationToken cancellationToken = default)
    {
        Current = Normalize(await apiClient.CreateAsync(request, cancellationToken));
        return Current;
    }

    public async Task<KnownHostInventoryResponse> UpdateAsync(Guid hostId, UpdateKnownHostRequest request, CancellationToken cancellationToken = default)
    {
        Current = Normalize(await apiClient.UpdateAsync(hostId, request, cancellationToken));
        return Current;
    }

    public async Task<KnownHostInventoryResponse> DeleteAsync(Guid hostId, CancellationToken cancellationToken = default)
    {
        Current = Normalize(await apiClient.DeleteAsync(hostId, cancellationToken));
        return Current;
    }

    private static KnownHostInventoryResponse Normalize(KnownHostInventoryResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.Hosts is null)
        {
            throw new ApiProtocolException("Known-host inventory response is missing the host list.");
        }

        List<KnownHostInventoryItem> hosts = new(response.Hosts.Count);
        foreach (KnownHostInventoryItem entry in response.Hosts)
        {
            if (entry is null
                || entry.Id == Guid.Empty
                || string.IsNullOrWhiteSpace(entry.Name)
                || !FirewallAddressValue.TryNormalizeLiteral(entry.Address, out string? normalizedAddress, out FirewallAddressFamily addressFamily)
                || addressFamily != entry.AddressFamily)
            {
                throw new ApiProtocolException("Known-host inventory response contains an invalid host entry.");
            }

            hosts.Add(new KnownHostInventoryItem
            {
                Id = entry.Id,
                Name = entry.Name.Trim(),
                Address = normalizedAddress,
                AddressFamily = addressFamily,
                Comment = string.IsNullOrWhiteSpace(entry.Comment) ? null : entry.Comment.Trim(),
                IsVisible = entry.IsVisible,
            });
        }

        if (hosts.Select(static entry => entry.Id).Distinct().Count() != hosts.Count
            || hosts.Select(static entry => entry.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != hosts.Count)
        {
            throw new ApiProtocolException("Known-host inventory response contains duplicate host identities.");
        }

        KnownHostInventoryItem[] ordered =
        [
            .. hosts
                .OrderByDescending(static entry => entry.IsVisible)
                .ThenBy(static entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static entry => entry.Name, StringComparer.Ordinal)
        ];

        return new KnownHostInventoryResponse { Hosts = ordered };
    }
}
