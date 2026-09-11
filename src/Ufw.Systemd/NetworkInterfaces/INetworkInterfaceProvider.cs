namespace Ufw.Systemd.NetworkInterfaces;

internal interface INetworkInterfaceProvider
{
    IReadOnlyList<string> GetInterfaceNames();
}
