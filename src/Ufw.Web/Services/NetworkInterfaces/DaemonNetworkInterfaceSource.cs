using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Data.Model;

namespace Ufw.Web.Services.NetworkInterfaces;

internal sealed class DaemonNetworkInterfaceSource(IUfwClient ufwClient) : IDaemonNetworkInterfaceSource
{
    public async Task<IReadOnlyList<string>> GetInterfaceNamesAsync(CancellationToken cancellationToken = default)
    {
        NetworkInterfaceListResponse response = await ufwClient.SendAsync<NetworkInterfaceListResponse>(RequestMethod.Get, "/api/v1/network-interfaces", cancellationToken);
        return ValidateAndOrderNames(response.Interfaces);
    }

    private static string[] ValidateAndOrderNames(IReadOnlyList<string>? names)
    {
        if (names is null)
        {
            throw new InvalidDataException("Daemon network-interface response is missing the interface list.");
        }
        if (names.Any(static name => string.IsNullOrWhiteSpace(name)))
        {
            throw new InvalidDataException("Daemon network-interface response contains an invalid interface name.");
        }
        if (names.Any(static name => name.Length > NetworkInterfaceEntry.MAX_NAME_LENGTH))
        {
            throw new InvalidDataException("Daemon returned a network-interface name that exceeds the supported length.");
        }

        string[] ordered = names.OrderBy(static name => name, StringComparer.Ordinal).ToArray();
        if (ordered.Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidDataException("Daemon network-interface response contains duplicate interface names.");
        }

        return ordered;
    }
}
