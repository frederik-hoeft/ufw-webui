namespace Ufw.Web.Client.Features.Status.Api;

internal interface IManagementApiHealthClient
{
    Task ProbeAsync(CancellationToken cancellationToken = default);
}
