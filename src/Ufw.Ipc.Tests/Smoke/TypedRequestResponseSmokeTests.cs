using Microsoft.Extensions.DependencyInjection;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Tests.Adapter;
using Ufw.Ipc.Tests.Adapter.Endpoints;
using Ufw.Systemd.Network;

namespace Ufw.Ipc.Tests.Smoke;

[TestClass]
public sealed class TypedRequestResponseSmokeTests : IpcProtocolTestBase
{
    protected override ValueTask ConfigureEndpointsAsync(ITestEndpointMapBuilder endpoints, CancellationToken cancellationToken)
    {
        endpoints.MapGet(
            "/api/v1/ping",
            static _ => ValueTask.FromResult<OkResponse>(new OkResponse()));

        endpoints.MapPost<EchoRequest, EchoResponse>(
            "/api/v1/echo",
            static (request, _) => ValueTask.FromResult(new EchoResponse(request.Message)));

        return ValueTask.CompletedTask;
    }

    [TestMethod]
    public Task Get_Ping_ReturnsOk() => RunAsync(async (context, cancellationToken) =>
    {
        OkResponse response = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/ping", cancellationToken);
        Assert.IsNotNull(response);
    }).AsTask();

    [TestMethod]
    public Task DefaultHost_UsesProductionNetworkApplication() => RunAsync((context, _) =>
    {
        INetworkApplication application = context.ServerServices.GetRequiredService<INetworkApplication>();
        INetworkApplicationWorker worker = context.ServerServices.GetRequiredService<INetworkApplicationWorker>();

        Assert.IsInstanceOfType<NetworkApplication>(application);
        Assert.IsInstanceOfType<NetworkApplicationWorker>(worker);
        return ValueTask.CompletedTask;
    }).AsTask();

    [TestMethod]
    public Task Post_Echo_RoundTripsPayload() => RunAsync(async (context, cancellationToken) =>
    {
        EchoResponse response = await context.SendAsync<EchoRequest, EchoResponse>(
            RequestMethod.Post,
            "/api/v1/echo",
            new EchoRequest("hello-ipc"),
            cancellationToken);

        Assert.AreEqual("hello-ipc", response.Message);
    }).AsTask();
}

file sealed record EchoRequest(string Message);

file sealed record EchoResponse(string Message) : OkResponseBase;
