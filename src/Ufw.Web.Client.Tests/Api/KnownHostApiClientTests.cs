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

        await client.GetAsync();
        await client.CreateAsync(new CreateKnownHostRequest { Name = "nas", Address = "192.0.2.10" });
        await client.UpdateAsync(id, new UpdateKnownHostRequest { Name = "nas", Address = "192.0.2.11" });
        await client.ReconcileDnsAsync(id);
        await client.DeleteAsync(id);

        CollectionAssert.AreEqual(
            new[]
            {
                "/api/v1/known-hosts",
                "/api/v1/known-hosts",
                $"/api/v1/known-hosts/{id:D}",
                $"/api/v1/known-hosts/{id:D}/dns/reconcile",
                $"/api/v1/known-hosts/{id:D}",
            },
            handler.Requests.Select(static request => request.RequestUri!.AbsolutePath).ToArray());
        CollectionAssert.AreEqual(
            new[] { HttpMethod.Get, HttpMethod.Post, HttpMethod.Put, HttpMethod.Post, HttpMethod.Delete },
            handler.Requests.Select(static request => request.Method).ToArray());
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.UpdateAsync(Guid.Empty, new UpdateKnownHostRequest()));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.ReconcileDnsAsync(Guid.Empty));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.DeleteAsync(Guid.Empty));
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
}
