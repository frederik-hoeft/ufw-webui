namespace Ufw.Systemd.NetworkInterfaces;

internal sealed record NetworkInterfaceSnapshot(bool IsAvailable, IReadOnlyList<string> Interfaces)
{
    public static NetworkInterfaceSnapshot Unavailable { get; } = new(false, []);

    public static NetworkInterfaceSnapshot Available(IReadOnlyList<string> interfaces)
    {
        ArgumentNullException.ThrowIfNull(interfaces);
        return new NetworkInterfaceSnapshot(true, interfaces);
    }
}
