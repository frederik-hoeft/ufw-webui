using System.Net;
using System.Text;
using System.Text.Json;
using Ufw.Client.Api;
using Ufw.Client.Tests.Support;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;

namespace Ufw.Client.Tests.Api;

[TestClass]
public sealed class HttpApiClientsTests
{
    [TestMethod]
    public async Task AuthApiClient_LoginRefreshAndLogoutUseExpectedEndpointsAsync()
    {
        using RecordingHttpMessageHandler handler = new((request, call) => call switch
        {
            1 => Json(HttpStatusCode.OK, "{\"accessToken\":\"login\",\"expiresAt\":\"2026-09-11T18:00:00+00:00\"}"),
            2 => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            3 => new HttpResponseMessage(HttpStatusCode.NoContent),
            _ => throw new InvalidOperationException(),
        });
        using HttpClient http = CreateClient(handler);
        AuthApiClient client = new(http);

        AuthTokenResponse login = await client.LoginAsync(new LoginRequest("admin@example.invalid", "secret"));
        AuthTokenResponse? refresh = await client.TryRefreshAsync();
        await client.LogoutAsync();

        Assert.AreEqual("login", login.AccessToken);
        Assert.IsNull(refresh);
        CollectionAssert.AreEqual(
            new[] { "/api/v1/auth/login", "/api/v1/auth/refresh", "/api/v1/auth/logout" },
            handler.Requests.Select(static request => request.RequestUri!.AbsolutePath).ToArray());
        Assert.IsTrue(handler.Requests.All(static request => request.Method == HttpMethod.Post));
        using JsonDocument body = JsonDocument.Parse(handler.Requests[0].Content!);
        Assert.AreEqual("admin@example.invalid", body.RootElement.GetProperty("email").GetString());
    }

    [TestMethod]
    public async Task AuthApiClient_LogoutFailureSurfacesApiRequestExceptionAsync()
    {
        using RecordingHttpMessageHandler handler = new((_, _) => Json(HttpStatusCode.Forbidden, "{\"detail\":\"logout denied\"}"));
        using HttpClient http = CreateClient(handler);
        AuthApiClient client = new(http);

        ApiRequestException exception = await Assert.ThrowsExactlyAsync<ApiRequestException>(() => client.LogoutAsync());

        Assert.AreEqual(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.AreEqual("logout denied", exception.Message);
    }

    [TestMethod]
    public async Task IntentAndRuleClients_UseExpectedMethodsPathsAndBodiesAsync()
    {
        using RecordingHttpMessageHandler intentHandler = new((_, _) => Json(HttpStatusCode.OK, $"{{\"protocolVersion\":{IntentProtocol.VERSION},\"deploymentId\":\"deployment\"}}"));
        using HttpClient intentHttp = CreateClient(intentHandler);
        IntentContextApiClient intentClient = new(intentHttp);
        Assert.AreEqual("deployment", (await intentClient.GetAsync()).DeploymentId);
        Assert.AreEqual(HttpMethod.Get, intentHandler.Requests[0].Method);
        Assert.AreEqual("/api/v1/intent/context", intentHandler.Requests[0].RequestUri!.AbsolutePath);

        using RecordingHttpMessageHandler rulesHandler = new((request, call) => call switch
        {
            1 => Json(HttpStatusCode.OK, "{\"active\":true,\"rules\":[]}"),
            2 => Json(HttpStatusCode.OK, MutationResponseJson(IntentOperations.ADD_RULE)),
            3 => Json(HttpStatusCode.OK, MutationResponseJson(IntentOperations.DELETE_RULE)),
            4 => Json(HttpStatusCode.OK, InsertionResponseJson(RuleInsertionOutcome.Completed)),
            5 => Json(HttpStatusCode.OK, ReorderResponseJson(RuleReorderOutcome.Completed)),
            _ => throw new InvalidOperationException(),
        });
        using HttpClient rulesHttp = CreateClient(rulesHandler);
        RuleApiClient rules = new(rulesHttp);
        await rules.GetRulesAsync();
        await rules.AddRuleAsync(AddRequest());
        await rules.DeleteRuleAsync(DeleteRequest());
        await rules.InsertRuleAsync(InsertRequest());
        await rules.ReorderRulesAsync(ReorderRequest());

        CollectionAssert.AreEqual(
            new[] { HttpMethod.Get, HttpMethod.Post, HttpMethod.Delete, HttpMethod.Post, HttpMethod.Put },
            rulesHandler.Requests.Select(static request => request.Method).ToArray());
        CollectionAssert.AreEqual(
            new[] { "/api/v1/rules", "/api/v1/rules", "/api/v1/rules", "/api/v1/rules/insert", "/api/v1/rules/order" },
            rulesHandler.Requests.Select(static request => request.RequestUri!.AbsolutePath).ToArray());
        Assert.IsTrue(rulesHandler.Requests.Skip(1).All(static request => request.Content is not null));
        using JsonDocument insertBody = JsonDocument.Parse(rulesHandler.Requests[3].Content!);
        Assert.AreEqual(IntentOperations.INSERT_RULE, insertBody.RootElement.GetProperty("operation").GetString());
        using JsonDocument reorderBody = JsonDocument.Parse(rulesHandler.Requests[4].Content!);
        Assert.AreEqual(IntentOperations.REORDER_RULES, reorderBody.RootElement.GetProperty("operation").GetString());
    }

    [TestMethod]
    public async Task RuleApiClient_InsertPreservesStructuredFailureButMapsSecurityConflictAsync()
    {
        using RecordingHttpMessageHandler handler = new((_, call) => call switch
        {
            1 => Json(HttpStatusCode.Conflict, InsertionResponseJson(RuleInsertionOutcome.StaleBaseline)),
            2 => Json(HttpStatusCode.Conflict, "{\"detail\":\"Intent nonce has already been used.\"}"),
            _ => throw new InvalidOperationException(),
        });
        using HttpClient http = CreateClient(handler);
        RuleApiClient client = new(http);

        RuleInsertionResponse report = await client.InsertRuleAsync(InsertRequest());
        ApiRequestException replay = await Assert.ThrowsExactlyAsync<ApiRequestException>(() => client.InsertRuleAsync(InsertRequest()));

        Assert.AreEqual(RuleInsertionOutcome.StaleBaseline, report.Outcome);
        Assert.AreEqual(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.AreEqual("Intent nonce has already been used.", replay.Message);
    }

    [TestMethod]
    public async Task RuleApiClient_InsertRejectsMalformedSuccessAsProtocolErrorAsync()
    {
        using RecordingHttpMessageHandler handler = new((_, _) => Json(HttpStatusCode.OK, "{\"detail\":\"not an insertion report\"}"));
        using HttpClient http = CreateClient(handler);
        RuleApiClient client = new(http);

        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => client.InsertRuleAsync(InsertRequest()));
    }

    [TestMethod]
    public async Task RuleApiClient_ReorderPreservesStructuredConflictButMapsSecurityConflictAsync()
    {
        using RecordingHttpMessageHandler handler = new((_, call) => call switch
        {
            1 => Json(HttpStatusCode.Conflict, ReorderResponseJson(RuleReorderOutcome.PartiallyCompleted)),
            2 => Json(HttpStatusCode.Conflict, "{\"detail\":\"Intent nonce has already been used.\"}"),
            _ => throw new InvalidOperationException(),
        });
        using HttpClient http = CreateClient(handler);
        RuleApiClient client = new(http);

        RuleReorderResponse report = await client.ReorderRulesAsync(ReorderRequest());
        ApiRequestException replay = await Assert.ThrowsExactlyAsync<ApiRequestException>(() => client.ReorderRulesAsync(ReorderRequest()));

        Assert.AreEqual(RuleReorderOutcome.PartiallyCompleted, report.Outcome);
        Assert.AreEqual(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.AreEqual("Intent nonce has already been used.", replay.Message);
    }

    [TestMethod]
    public async Task RuleApiClient_ReorderRejectsMalformedSuccessAsProtocolErrorAsync()
    {
        using RecordingHttpMessageHandler handler = new((_, _) => Json(HttpStatusCode.OK, "{\"detail\":\"not a reorder report\"}"));
        using HttpClient http = CreateClient(handler);
        RuleApiClient client = new(http);

        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => client.ReorderRulesAsync(ReorderRequest()));
    }

    [TestMethod]
    public async Task NetworkInterfaceApiClient_UsesResourceSpecificEndpointsAndValidatesIdsAsync()
    {
        Guid id = Guid.Parse("01993b41-fdad-7000-8000-000000000001");
        const string RESPONSE_JSON = "{\"interfaces\":[],\"reconciledAt\":null}";
        using RecordingHttpMessageHandler handler = new((_, _) => Json(HttpStatusCode.OK, RESPONSE_JSON));
        using HttpClient http = CreateClient(handler);
        NetworkInterfaceApiClient client = new(http);

        await client.GetAsync();
        await client.ReconcileAsync();
        await client.UpdateCommentAsync(id, "note");
        await client.UpdateVisibilityAsync(id, false);

        CollectionAssert.AreEqual(
            new[]
            {
                "/api/v1/network-interfaces",
                "/api/v1/network-interfaces/reconcile",
                $"/api/v1/network-interfaces/{id:D}/comment",
                $"/api/v1/network-interfaces/{id:D}/visibility",
            },
            handler.Requests.Select(static request => request.RequestUri!.AbsolutePath).ToArray());
        CollectionAssert.AreEqual(new[] { HttpMethod.Get, HttpMethod.Post, HttpMethod.Put, HttpMethod.Put }, handler.Requests.Select(static request => request.Method).ToArray());
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.UpdateCommentAsync(Guid.Empty, "note"));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.UpdateVisibilityAsync(Guid.Empty, true));
        Assert.HasCount(4, handler.Requests);
    }

    private static HttpClient CreateClient(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("https://localhost/"),
    };

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static AddRuleRequest AddRequest() => new()
    {
        DeploymentId = "deployment",
        KeyId = "key-id",
        Nonce = "nonce",
        Operation = IntentOperations.ADD_RULE,
        Payload = JsonSerializer.SerializeToElement(new { rule = new FirewallRuleSpecification() }),
        Signature = "signature",
    };

    private static InsertRuleRequest InsertRequest() => new()
    {
        DeploymentId = "deployment",
        KeyId = "key-id",
        Nonce = "nonce",
        Operation = IntentOperations.INSERT_RULE,
        Payload = JsonSerializer.SerializeToElement(new
        {
            baselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: true, []),
            anchorOccurrenceId = 0,
            placement = RuleInsertionPlacement.Before,
            rule = new FirewallRuleSpecification { AddressFamily = FirewallAddressFamily.IPv4 },
        }),
        Signature = "signature",
    };

    private static ReorderRulesRequest ReorderRequest() => new()
    {
        DeploymentId = "deployment",
        KeyId = "key-id",
        Nonce = "nonce",
        Operation = IntentOperations.REORDER_RULES,
        Payload = JsonSerializer.SerializeToElement(new
        {
            baselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: true, []),
            desiredOrder = Array.Empty<int>(),
        }),
        Signature = "signature",
    };

    private static DeleteRuleRequest DeleteRequest() => new()
    {
        DeploymentId = "deployment",
        KeyId = "key-id",
        Nonce = "nonce",
        Operation = IntentOperations.DELETE_RULE,
        Payload = JsonSerializer.SerializeToElement(new { ruleId = "id", rule = new FirewallRuleSpecification() }),
        Signature = "signature",
    };

    private static string InsertionResponseJson(RuleInsertionOutcome outcome)
    {
        ListedFirewallRule? inserted = outcome == RuleInsertionOutcome.Completed
            ? new ListedFirewallRule
            {
                RuleId = "inserted",
                DisplayNumber = 1,
                Parsed = true,
                RawLine = "line",
                Rule = new FirewallRuleSpecification { AddressFamily = FirewallAddressFamily.IPv4 },
            }
            : null;
        return JsonSerializer.Serialize(
            new RuleInsertionResponse(outcome, new RuleListResponse(true, inserted is null ? [] : [inserted]), inserted, Diagnostic: null),
            MessageJsonSerializerContext.Default.RuleInsertionResponse);
    }

    private static string ReorderResponseJson(RuleReorderOutcome outcome) => JsonSerializer.Serialize(
        new RuleReorderResponse(outcome, new RuleListResponse(true, []), [], [], [], Diagnostic: null),
        MessageJsonSerializerContext.Default.RuleReorderResponse);

    private static string MutationResponseJson(string operation) =>
        $"{{\"operation\":\"{operation}\",\"rule\":{{\"ruleId\":\"id\",\"displayNumber\":1,\"parsed\":true,\"rawLine\":\"line\",\"rule\":null}}}}";
}
