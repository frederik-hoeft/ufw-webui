using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Data.Model;
using Ufw.Web.Services.Daemon;

namespace Ufw.Web.Services.NetworkInterfaces;

internal sealed class NetworkInterfaceDaemonGateway(IUfwClient ufwClient) : INetworkInterfaceDaemonGateway
{
    private const string NETWORK_INTERFACES_ROUTE = "/api/v1/network-interfaces";

    public async Task<DaemonResult<IReadOnlyList<string>>> GetInterfaceNamesAsync(CancellationToken cancellationToken = default)
    {
        DaemonResult<NetworkInterfaceListResponse> daemonResult = await DaemonResult.FromIpcAsync(
            () => ufwClient.TrySendAsync<NetworkInterfaceListResponse>(RequestMethod.Get, NETWORK_INTERFACES_ROUTE, cancellationToken));
        if (!daemonResult.TryGetResult(out NetworkInterfaceListResponse? response, out UfwIpcError? error))
        {
            return DaemonResult.Failure<IReadOnlyList<string>>(error);
        }

        IReadOnlyList<string> names = ValidateAndOrderNames(response.Interfaces);
        return DaemonResult.Success(names);
    }

    private static IReadOnlyList<string> ValidateAndOrderNames(IReadOnlyList<string>? names)
    {
        if (names is null)
        {
            throw new DaemonInvalidResponseException("Daemon network-interface response is missing the interface list.");
        }
        if (names.Any(static name => string.IsNullOrWhiteSpace(name)))
        {
            throw new DaemonInvalidResponseException("Daemon network-interface response contains an invalid interface name.");
        }
        if (names.Any(static name => name.Length > NetworkInterfaceEntry.MAX_NAME_LENGTH))
        {
            throw new DaemonInvalidResponseException("Daemon returned a network-interface name that exceeds the supported length.");
        }

        string[] ordered = names.OrderBy(static name => name, StringComparer.Ordinal).ToArray();
        if (ordered.Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new DaemonInvalidResponseException("Daemon network-interface response contains duplicate interface names.");
        }

        return ordered;
    }
}
