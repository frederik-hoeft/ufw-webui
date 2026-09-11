namespace Ufw.Web.Services.NetworkInterfaces;

internal interface IDaemonNetworkInterfaceSource
{
    Task<IReadOnlyList<string>> GetInterfaceNamesAsync(CancellationToken cancellationToken = default);
}
