using Ufw.Web.Client.Infrastructure.Http;
namespace Ufw.Web.Client.Features.Status.Api;

internal sealed class ManagementApiHealthClient(HttpClient httpClient) : IManagementApiHealthClient
{
    private static readonly Uri s_healthUri = new("api/health", UriKind.Relative);

    public async Task ProbeAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(s_healthUri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await response.CreateExceptionAsync(cancellationToken);
        }
    }
}
