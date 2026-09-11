namespace Ufw.Systemd.NetworkInterfaces;

internal interface INetworkInterfaceSnapshotService
{
    NetworkInterfaceSnapshot GetSnapshot();
}
