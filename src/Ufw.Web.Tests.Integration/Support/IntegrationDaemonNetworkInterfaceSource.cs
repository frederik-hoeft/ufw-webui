using Ufw.Web.Services.NetworkInterfaces;

namespace Ufw.Web.Tests.Integration.Support;

internal sealed class IntegrationDaemonNetworkInterfaceSource : IDaemonNetworkInterfaceSource
{
    private IReadOnlyList<string> _interfaceNames = [];

    public Task<IReadOnlyList<string>> GetInterfaceNamesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_interfaceNames);
    }

    public void SetInterfaceNames(params string[] interfaceNames)
    {
        ArgumentNullException.ThrowIfNull(interfaceNames);
        _interfaceNames = interfaceNames;
    }
}
