namespace Ufw.Web.Client.Features.Status.Api;

internal interface IDaemonStatusApiClient
{
    Task ProbeAsync(CancellationToken cancellationToken = default);
}
