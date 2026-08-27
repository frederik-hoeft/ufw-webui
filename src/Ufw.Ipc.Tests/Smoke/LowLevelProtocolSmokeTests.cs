using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Tests.Adapter;
using Ufw.Ipc.Tests.Adapter.Endpoints;
using Ufw.Ipc.Tests.Adapter.Hosting;
using Ufw.Systemd.Network;

namespace Ufw.Ipc.Tests.Smoke;

[TestClass]
public sealed class LowLevelProtocolSmokeTests : IpcProtocolTestBase
{
    protected override ValueTask ConfigureEndpointsAsync(ITestEndpointMapBuilder endpoints, CancellationToken cancellationToken)
    {
        endpoints.MapGet(
            "/api/v1/raw-ok",
            static _ => ValueTask.FromResult(new OkResponse()));
        return ValueTask.CompletedTask;
    }

    [TestMethod]
    public Task ExchangeRaw_UsesProductionFraming() => RunAsync(async (context, cancellationToken) =>
    {
        await using IMessage request = await context.MessageSerializer.SerializeAsync(
            id: "/api/v1/raw-ok",
            method: RequestMethod.Get.ToString(),
            payload: (object?)null,
            type: typeof(object),
            cancellationToken);

        await using IMessage response = await context.ExchangeRawAsync(request, cancellationToken);

        Assert.AreEqual("200", response.Id);
    }).AsTask();

    [TestMethod]
    public Task MissingMethod_ValidationMiddleware_ReturnsBadRequest() => RunAsync(async (context, cancellationToken) =>
    {
        await using IMessage request = await context.MessageSerializer.SerializeAsync(
            id: "/api/v1/raw-ok",
            method: null,
            payload: (object?)null,
            type: typeof(object),
            cancellationToken);

        await using IMessage response = await context.ExchangeRawAsync(request, cancellationToken);

        Assert.AreEqual("400", response.Id);
        BadRequestResponse? body = await response.Payload.ReadAsync<BadRequestResponse>(cancellationToken);
        Assert.IsNotNull(body);
        StringAssert.Contains(body.Message, "Malformed request");
    }).AsTask();

    [TestMethod]
    public Task MalformedHeaderBytes_CanUseResilientTestWorker() => RunAsync(
        async (context, cancellationToken) =>
        {
            ReadOnlyMemory<byte> garbage = Encoding.UTF8.GetBytes("{not-json\n{}\n");

            Exception exception = await Assert.ThrowsAsync<Exception>(async () =>
            {
                await using IMessage _ = await context.ExchangeBytesAsync(garbage, cancellationToken);
            });
            Assert.IsTrue(
                exception is InvalidDataException
                    or IOException
                    or System.Text.Json.JsonException
                    or EndOfStreamException
                    or OperationCanceledException,
                $"Unexpected exception type: {exception.GetType().FullName}: {exception.Message}");

            OkResponse response = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/raw-ok", cancellationToken);
            Assert.IsNotNull(response);
        },
        configuration: new IpcTestRunConfiguration
        {
            ConfigureServerServices = static services =>
                services.Replace(ServiceDescriptor.Transient<INetworkApplicationWorker, ResilientIpcTestServerWorker>()),
        }).AsTask();
}
