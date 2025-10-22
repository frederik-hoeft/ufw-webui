using System.Net.NetworkInformation;

namespace UfwWebUI.Services;

public class NetworkInterfaceService : INetworkInterfaceService
{
    public Task<IList<string>> GetNetworkInterfacesAsync()
    {
        IList<string> interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up && 
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(ni => ni.Name)
            .OrderBy(name => name)
            .ToList();

        return Task.FromResult(interfaces);
    }
}
