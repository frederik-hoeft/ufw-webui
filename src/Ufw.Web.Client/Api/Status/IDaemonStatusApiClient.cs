namespace Ufw.Web.Client.Api.Status;

internal interface IDaemonStatusApiClient
{
    Task ProbeAsync(CancellationToken cancellationToken = default);
}
