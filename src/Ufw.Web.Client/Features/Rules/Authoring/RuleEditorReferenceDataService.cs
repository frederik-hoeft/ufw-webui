using Ufw.Shared.Firewall;
using Ufw.Shared.Management.KnownHosts;
using Ufw.Shared.Management.NetworkInterfaces;
using Ufw.Web.Client.Features.KnownHosts;
using Ufw.Web.Client.Features.NetworkInterfaces;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Model.V1.KnownHosts;
using Ufw.Web.Model.V1.NetworkInterfaces;

namespace Ufw.Web.Client.Features.Rules.Authoring;

internal sealed class RuleEditorReferenceDataService(
    IKnownHostInventoryService knownHosts,
    INetworkInterfaceInventoryService networkInterfaces,
    IClientErrorMapper errors) : IRuleEditorReferenceDataService
{
    public async Task<RuleEditorReferenceData> LoadAsync(CancellationToken cancellationToken = default)
    {
        Task<RuleEditorCatalogResult<KnownHostInventoryItem>> hostsTask = LoadKnownHostsAsync(cancellationToken);
        Task<RuleEditorCatalogResult<NetworkInterfaceInventoryItem>> interfacesTask = LoadInterfacesAsync(cancellationToken);
        await Task.WhenAll(hostsTask, interfacesTask);

        RuleEditorCatalogResult<KnownHostInventoryItem> hosts = await hostsTask;
        RuleEditorCatalogResult<NetworkInterfaceInventoryItem> interfaces = await interfacesTask;
        return new RuleEditorReferenceData(hosts, interfaces);
    }

    public IReadOnlyList<KnownHostInventoryItem> GetVisibleKnownHosts(RuleEditorReferenceData data, bool ipv6Enabled)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.KnownHosts.Items
            .Where(host => host.IsVisible && (ipv6Enabled || host.AddressFamily != FirewallAddressFamily.IPv6))
            .ToArray();
    }

    public bool IsUnknownInterface(RuleEditorReferenceData data, string? interfaceName)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.Interfaces.Error is null
            && !string.IsNullOrWhiteSpace(interfaceName)
            && !data.Interfaces.Items.Any(candidate => string.Equals(candidate.Name, interfaceName, StringComparison.Ordinal));
    }

    private async Task<RuleEditorCatalogResult<KnownHostInventoryItem>> LoadKnownHostsAsync(CancellationToken cancellationToken)
    {
        try
        {
            KnownHostInventoryResponse inventory = await knownHosts.RefreshAsync(cancellationToken);
            return RuleEditorCatalogResult<KnownHostInventoryItem>.Loaded(inventory.Hosts);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (errors.CanDescribe(exception))
        {
            ClientError error = errors.Describe(exception);
            return RuleEditorCatalogResult<KnownHostInventoryItem>.Failed(error);
        }
    }

    private async Task<RuleEditorCatalogResult<NetworkInterfaceInventoryItem>> LoadInterfacesAsync(CancellationToken cancellationToken)
    {
        try
        {
            NetworkInterfaceInventoryResponse inventory = await networkInterfaces.RefreshAsync(cancellationToken);
            return RuleEditorCatalogResult<NetworkInterfaceInventoryItem>.Loaded(inventory.Interfaces);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (errors.CanDescribe(exception))
        {
            ClientError error = errors.Describe(exception);
            return RuleEditorCatalogResult<NetworkInterfaceInventoryItem>.Failed(error);
        }
    }
}
