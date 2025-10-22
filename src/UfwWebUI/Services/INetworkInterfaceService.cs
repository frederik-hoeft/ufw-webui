namespace UfwWebUI.Services;

public interface INetworkInterfaceService
{
    Task<IList<string>> GetNetworkInterfacesAsync();
}
