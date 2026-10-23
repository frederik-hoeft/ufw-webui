using System.Net;
using System.Text;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.Tests.Api;

[TestClass]
public sealed class KnownHostApiClientTests
{
    [TestMethod]
    public async Task CrudMethods_UseKnownHostResourceEndpointsAndValidateIdsAsync()
    {
        const string RESPONSE_JSON = "{\"hosts\":[]}";
        Guid id = Guid.Parse("01993b41-fdad-7000-8000-000000000002");
        using RecordingHandler handler = new(RESPONSE_JSON);
        using HttpClient http = new(handler) { BaseAddress = new Uri("https://localhost/") };
        KnownHostApiClient client = new(http);

        await client.GetAsync(TestContext.CancellationToken);
        await client.CreateAsync(new CreateKnownHostRequest { Name = "nas", Address = "192.0.2.10" }, TestContext.CancellationToken);
        await client.UpdateAsync(id, new UpdateKnownHostRequest { Name = "nas", Address = "192.0.2.11" }, TestContext.CancellationToken);
        await client.ReconcileDnsAsync(id, TestContext.CancellationToken);
        await client.DeleteAsync(id, TestContext.CancellationToken);

        Assert.AreSequenceEqual(
            [
                "/api/v1/known-hosts",
                "/api/v1/known-hosts",
                $"/api/v1/known-hosts/{id:D}",
                $"/api/v1/known-hosts/{id:D}/dns/reconcile",
                $"/api/v1/known-hosts/{id:D}",
            ], handler.Requests.Select(static request => request.RequestUri!.AbsolutePath).ToArray());
        Assert.AreSequenceEqual(
            [HttpMethod.Get, HttpMethod.Post, HttpMethod.Put, HttpMethod.Post, HttpMethod.Delete], handler.Requests.Select(static request => request.Method).ToArray());
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.UpdateAsync(Guid.Empty, new UpdateKnownHostRequest(), TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.ReconcileDnsAsync(Guid.Empty, TestContext.CancellationToken));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.DeleteAsync(Guid.Empty, TestContext.CancellationToken));
        Assert.HasCount(5, handler.Requests);
    }

    private sealed class RecordingHandler(string responseJson) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            });
        }
    }

    public TestContext TestContext { get; set; }
}
