using Ufw.Shared.Firewall;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Model.V1.KnownHosts;
using Ufw.Web.Client.Features.KnownHosts;
using Ufw.Web.Client.Api.NetworkInterfaces;
using Ufw.Web.Model.V1.NetworkInterfaces;
using Ufw.Web.Client.Features.NetworkInterfaces;
using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.Features.Rules.Authoring;

internal sealed class RuleEditorReferenceDataService(
    IKnownHostInventoryService knownHosts,
    INetworkInterfaceInventoryService networkInterfaces,
    IClientErrorMapper errors) : IRuleEditorReferenceDataService
{
    public async Task<RuleEditorReferenceData> LoadAsync(CancellationToken cancellationToken = default)
    {
        Task<IReadOnlyList<KnownHostInventoryItem>> hostsTask = LoadKnownHostsAsync(cancellationToken);
        Task<InterfaceInventoryResult> interfacesTask = LoadInterfacesAsync(cancellationToken);
        await Task.WhenAll(hostsTask, interfacesTask);

        InterfaceInventoryResult interfaces = await interfacesTask;
        return new RuleEditorReferenceData(await hostsTask, interfaces.All, interfaces.Visible, interfaces.Error);
    }

    public IReadOnlyList<KnownHostInventoryItem> GetVisibleKnownHosts(RuleEditorReferenceData data, bool ipv6Enabled)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.KnownHosts
            .Where(host => host.IsVisible && (ipv6Enabled || host.AddressFamily != FirewallAddressFamily.IPv6))
            .ToArray();
    }

    public bool IsUnknownInterface(RuleEditorReferenceData data, string? interfaceName)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.InterfaceInventoryError is null
            && !string.IsNullOrWhiteSpace(interfaceName)
            && !data.KnownInterfaces.Any(candidate => string.Equals(candidate.Name, interfaceName, StringComparison.Ordinal));
    }

    private async Task<IReadOnlyList<KnownHostInventoryItem>> LoadKnownHostsAsync(CancellationToken cancellationToken)
    {
        try
        {
            KnownHostInventoryResponse inventory = await knownHosts.RefreshAsync(cancellationToken);
            return inventory.Hosts;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (errors.TryDescribe(exception, out _))
        {
            _ = errors.Describe(exception);
            return [];
        }
    }

    private async Task<InterfaceInventoryResult> LoadInterfacesAsync(CancellationToken cancellationToken)
    {
        try
        {
            NetworkInterfaceInventoryResponse inventory = await networkInterfaces.RefreshAsync(cancellationToken);
            return new InterfaceInventoryResult(
                inventory.Interfaces,
                inventory.Interfaces.Where(static networkInterface => networkInterface.IsVisible).ToArray(),
                null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new InterfaceInventoryResult([], [], errors.Describe(exception).Message);
        }
    }

    private sealed record InterfaceInventoryResult(IReadOnlyList<NetworkInterfaceInventoryItem> All, IReadOnlyList<NetworkInterfaceInventoryItem> Visible, string? Error);
}
