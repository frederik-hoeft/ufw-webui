using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Tests.Adapter;

namespace Ufw.Ipc.Tests.Smoke;

[TestClass]
public sealed class RoutingSmokeTests : IpcProtocolTestBase
{
    [TestMethod]
    public Task TestUnknownRoute_ReturnsNotFound() => RunAsync(
        configureEndpoints: static endpoints => endpoints
            .MapGet("/api/v1/known", static _ => ValueTask.FromResult(new OkResponse())),
        actAsync: async (context, cancellationToken) =>
        {
            InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
                _ = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/missing", cancellationToken));

            Assert.Contains("404", exception.Message);
        }, cancellationToken: TestContext.CancellationToken).AsTask();

    [TestMethod]
    public Task TestPerTestEndpoint_OverridesClassMapIsolation() => RunAsync(
        configureEndpoints: static endpoints => endpoints
            .MapGet("/api/v1/ephemeral", static _ => ValueTask.FromResult(new OkResponse())),
        actAsync: async (context, cancellationToken) =>
        {
            OkResponse response = await context.SendAsync<OkResponse>(
                RequestMethod.Get,
                "/api/v1/ephemeral",
                cancellationToken);
            Assert.IsNotNull(response);
        }, cancellationToken: TestContext.CancellationToken).AsTask();

    [TestMethod]
    public Task TestPipelineOnly_MatchUnsupportedMethod() => RunAsync(async (context, cancellationToken) =>
    {
        await using IRequestMessage request = await context.MessageSerializer
            .SerializeRequestAsync(route: "/api/v1/anything", method: "PATCH", cancellationToken);

        await using IResponseMessage response = await context.ProcessPipelineAsync(request, cancellationToken);

        Assert.AreEqual(501, response.StatusCode);
    }, cancellationToken: TestContext.CancellationToken).AsTask();
}
