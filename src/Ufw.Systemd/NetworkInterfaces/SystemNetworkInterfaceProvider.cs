using System.Net.NetworkInformation;

namespace Ufw.Systemd.NetworkInterfaces;

internal sealed class SystemNetworkInterfaceProvider : INetworkInterfaceProvider
{
    public IReadOnlyList<string> GetInterfaceNames() => NetworkInterface.GetAllNetworkInterfaces()
        .Select(static networkInterface => networkInterface.Name)
        .Where(static name => !string.IsNullOrWhiteSpace(name))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(static name => name, StringComparer.Ordinal)
        .ToArray();
}
