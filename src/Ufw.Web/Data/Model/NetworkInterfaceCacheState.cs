namespace Ufw.Web.Data.Model;

internal sealed partial class NetworkInterfaceCacheState
{
    public const int SINGLETON_ID = 1;

    public int Id { get; set; } = SINGLETON_ID;

    public DateTimeOffset ReconciledAt { get; set; }
}
