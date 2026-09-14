namespace Ufw.Client.Api;

internal interface IDaemonStatusApiClient
{
    Task ProbeAsync(CancellationToken cancellationToken = default);
}
