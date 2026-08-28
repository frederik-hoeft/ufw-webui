using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Tests.Adapter;
using Ufw.Ipc.Tests.Adapter.Endpoints;

namespace Ufw.Ipc.Tests.Smoke;

[TestClass]
public sealed class RoutingSmokeTests : IpcProtocolTestBase
{
    [TestMethod]
    public Task UnknownRoute_ReturnsNotFound() => RunAsync(
        configureEndpoints: static endpoints =>
        {
            endpoints.MapGet(
                "/api/v1/known",
                static _ => ValueTask.FromResult<OkResponse>(new OkResponse()));
        },
        actAsync: async (context, cancellationToken) =>
        {
            InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            {
                _ = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/missing", cancellationToken);
            });

            StringAssert.Contains(exception.Message, "404");
        }).AsTask();

    [TestMethod]
    public Task PerTestEndpoint_OverridesClassMapIsolation() => RunAsync(
        configureEndpoints: static endpoints =>
        {
            endpoints.MapGet(
                "/api/v1/ephemeral",
                static _ => ValueTask.FromResult(new OkResponse()));
        },
        actAsync: async (context, cancellationToken) =>
        {
            OkResponse response = await context.SendAsync<OkResponse>(
                RequestMethod.Get,
                "/api/v1/ephemeral",
                cancellationToken);
            Assert.IsNotNull(response);
        }).AsTask();

    [TestMethod]
    public Task PipelineOnly_MatchUnsupportedMethod() => RunAsync(async (context, cancellationToken) =>
    {
        await using IMessage request = await context.MessageSerializer
            .SerializeAsync(id: "/api/v1/anything", method: "PATCH", payload: (object?)null, typeof(object), cancellationToken);

        await using IMessage response = await context.ProcessPipelineAsync(request, cancellationToken);

        Assert.AreEqual("501", response.Id);
    }).AsTask();
}
