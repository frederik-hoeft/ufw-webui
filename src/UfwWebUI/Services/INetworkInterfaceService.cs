namespace UfwWebUI.Services;

public interface INetworkInterfaceService
{
    Task<List<string>> GetNetworkInterfacesAsync();
}
