namespace Ufw.Web.Client.Api;

internal interface IManagementApiHealthClient
{
    Task ProbeAsync(CancellationToken cancellationToken = default);
}
