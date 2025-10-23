namespace UfwWebUI.Services;

internal interface INetworkInterfaceService
{
    Task<List<string>> GetNetworkInterfacesAsync();
}
