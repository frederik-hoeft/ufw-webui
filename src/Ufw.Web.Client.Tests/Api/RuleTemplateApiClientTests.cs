using System.Net;
using System.Text;
using Ufw.Web.Client.Api.RuleTemplates;
using Ufw.Web.Model.V1.RuleTemplates;

namespace Ufw.Web.Client.Tests.Api;

[TestClass]
public sealed class RuleTemplateApiClientTests
{
    [TestMethod]
    public async Task CrudMethods_UseRuleTemplateResourceEndpointsAndValidateIdsAsync()
    {
        const string RESPONSE_JSON = "{\"templates\":[]}";
        Guid id = Guid.Parse("01993b41-fdad-7000-8000-000000000004");
        using RecordingHandler handler = new(RESPONSE_JSON);
        using HttpClient http = new(handler) { BaseAddress = new Uri("https://localhost/") };
        RuleTemplateApiClient client = new(http);

        await client.GetAsync();
        await client.CreateAsync(new CreateRuleTemplateRequest { Name = "web" });
        await client.UpdateAsync(id, new UpdateRuleTemplateRequest { Name = "web-updated" });
        await client.DeleteAsync(id);

        CollectionAssert.AreEqual(
            new[]
            {
                "/api/v1/rule-templates",
                "/api/v1/rule-templates",
                $"/api/v1/rule-templates/{id:D}",
                $"/api/v1/rule-templates/{id:D}",
            },
            handler.Requests.Select(static request => request.RequestUri!.AbsolutePath).ToArray());
        CollectionAssert.AreEqual(
            new[] { HttpMethod.Get, HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete },
            handler.Requests.Select(static request => request.Method).ToArray());
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.UpdateAsync(Guid.Empty, new UpdateRuleTemplateRequest()));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.DeleteAsync(Guid.Empty));
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
