using Ufw.Web.Client.Infrastructure.Http;
namespace Ufw.Web.Client.Features.Status.Api;

internal sealed class DaemonStatusApiClient(HttpClient httpClient) : IDaemonStatusApiClient
{
    private static readonly Uri s_statusUri = new("api/v1/status", UriKind.Relative);

    public async Task ProbeAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(s_statusUri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await response.CreateExceptionAsync(cancellationToken);
        }
    }
}
