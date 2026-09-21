using Ufw.Web.Client.Api;
namespace Ufw.Web.Client.Api.Status;

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
