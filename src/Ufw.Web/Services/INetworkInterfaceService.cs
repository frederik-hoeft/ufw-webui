namespace Ufw.Web.Services;

internal interface INetworkInterfaceService
{
    Task<List<string>> GetNetworkInterfacesAsync();
}
