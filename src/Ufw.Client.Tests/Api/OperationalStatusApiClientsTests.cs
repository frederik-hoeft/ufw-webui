using System.Net;
using Ufw.Client.Api;
using Ufw.Client.Tests.Support;

namespace Ufw.Client.Tests.Api;

[TestClass]
public sealed class OperationalStatusApiClientsTests
{
    [TestMethod]
    public async Task ManagementAndDaemonStatusClients_UseDedicatedProbeEndpointsAsync()
    {
        using RecordingHttpMessageHandler managementHandler = new((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        using HttpClient managementHttp = CreateClient(managementHandler);
        ManagementApiHealthClient management = new(managementHttp);

        using RecordingHttpMessageHandler daemonHandler = new((_, _) => new HttpResponseMessage(HttpStatusCode.NoContent));
        using HttpClient daemonHttp = CreateClient(daemonHandler);
        DaemonStatusApiClient daemon = new(daemonHttp);

        await management.ProbeAsync();
        await daemon.ProbeAsync();

        Assert.AreEqual(HttpMethod.Get, managementHandler.Requests[0].Method);
        Assert.AreEqual("/api/health", managementHandler.Requests[0].RequestUri!.AbsolutePath);
        Assert.AreEqual(HttpMethod.Get, daemonHandler.Requests[0].Method);
        Assert.AreEqual("/api/v1/status", daemonHandler.Requests[0].RequestUri!.AbsolutePath);
    }

    [TestMethod]
    public async Task ProbeClients_SurfaceNonSuccessResponsesAsync()
    {
        using RecordingHttpMessageHandler handler = new((_, _) => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using HttpClient http = CreateClient(handler);
        ManagementApiHealthClient client = new(http);

        ApiRequestException exception = await Assert.ThrowsExactlyAsync<ApiRequestException>(() => client.ProbeAsync());

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    private static HttpClient CreateClient(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("https://localhost/"),
    };
}
