using Ufw.Shared.Firewall;
using Ufw.Shared.Management.KnownHosts;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.Features.KnownHosts;

internal sealed class KnownHostInventoryService(IKnownHostApiClient apiClient) : IKnownHostInventoryService
{
    public KnownHostInventoryResponse? Current { get; private set; }

    public async Task<KnownHostInventoryResponse> RefreshAsync(CancellationToken cancellationToken = default)
    {
        KnownHostInventoryResponse response = await apiClient.GetAsync(cancellationToken);
        Current = Normalize(response);
        return Current;
    }

    public async Task<KnownHostInventoryResponse> CreateAsync(CreateKnownHostRequest request, CancellationToken cancellationToken = default)
    {
        KnownHostInventoryResponse response = await apiClient.CreateAsync(request, cancellationToken);
        Current = Normalize(response);
        return Current;
    }

    public async Task<KnownHostInventoryResponse> UpdateAsync(Guid hostId, UpdateKnownHostRequest request, CancellationToken cancellationToken = default)
    {
        KnownHostInventoryResponse response = await apiClient.UpdateAsync(hostId, request, cancellationToken);
        Current = Normalize(response);
        return Current;
    }

    public async Task<KnownHostInventoryResponse> ReconcileDnsAsync(Guid hostId, CancellationToken cancellationToken = default)
    {
        KnownHostInventoryResponse response = await apiClient.ReconcileDnsAsync(hostId, cancellationToken);
        Current = Normalize(response);
        return Current;
    }

    public async Task<KnownHostInventoryResponse> DeleteAsync(Guid hostId, CancellationToken cancellationToken = default)
    {
        KnownHostInventoryResponse response = await apiClient.DeleteAsync(hostId, cancellationToken);
        Current = Normalize(response);
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
                || addressFamily != entry.AddressFamily
                || !Enum.IsDefined(entry.AddressSource)
                || entry.AddressSource == KnownHostAddressSource.Literal && entry.DnsResolvedAt is not null
                || entry.AddressSource == KnownHostAddressSource.Dns && entry.DnsResolvedAt is null)
            {
                throw new ApiProtocolException("Known-host inventory response contains an invalid host entry.");
            }

            hosts.Add(new KnownHostInventoryItem
            {
                Id = entry.Id,
                Name = entry.Name.Trim(),
                Address = normalizedAddress,
                AddressFamily = addressFamily,
                AddressSource = entry.AddressSource,
                DnsResolvedAt = entry.DnsResolvedAt,
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
