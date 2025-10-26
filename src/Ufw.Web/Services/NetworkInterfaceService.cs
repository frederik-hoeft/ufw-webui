using System.Net.NetworkInformation;

namespace Ufw.Web.Services;

internal sealed class NetworkInterfaceService : INetworkInterfaceService
{
    public Task<List<string>> GetNetworkInterfacesAsync()
    {
        List<string> interfaces = 
        [
            .. NetworkInterface.GetAllNetworkInterfaces()
                .Where(static ni => ni is 
                { 
                    OperationalStatus: OperationalStatus.Up,
                    NetworkInterfaceType: not NetworkInterfaceType.Loopback 
                })
                .Select(static ni => ni.Name)
                .OrderBy(static name => name)
        ];

        return Task.FromResult(interfaces);
    }
}
