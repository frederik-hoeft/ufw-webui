using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Protocol;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Shared.Transport.Itp;
using Ufw.Ipc.Shared.Transport.Security;
using Ufw.Ipc.Tests.Adapter;
using Ufw.Ipc.Tests.Adapter.Endpoints;

namespace Ufw.Ipc.Tests.Smoke;

[TestClass]
public sealed class LowLevelProtocolSmokeTests : IpcProtocolTestBase
{
    protected override ValueTask ConfigureOptionsAsync(IpcTestOptions options, CancellationToken cancellationToken)
    {
        options.WorkerCount = 1;
        return ValueTask.CompletedTask;
    }

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
        Assert.AreEqual(ApplicationPayloadTypes.Error, response.PayloadType);
        BadRequestResponse? body = await response.Payload.ReadAsync<BadRequestResponse>(cancellationToken);
        Assert.IsNotNull(body);
        Assert.IsFalse(string.IsNullOrWhiteSpace(body.Message));
    }).AsTask();

    [TestMethod]
    public Task MalformedHeaderBytes_DoesNotTerminateProductionWorker() => RunAsync(async (context, cancellationToken) =>
    {
        ReadOnlyMemory<byte> garbage = Encoding.UTF8.GetBytes("{not-json\n{}\n");

        Exception exception = await Assert.ThrowsAsync<Exception>(async () =>
        {
            await using IMessage _ = await context.ExchangeBytesAsync(garbage, cancellationToken);
        });
        Assert.IsTrue(
            exception is ItpException
                or InvalidDataException
                or IOException
                or System.Text.Json.JsonException
                or EndOfStreamException
                or OperationCanceledException
                or TimeoutException,
            $"Unexpected exception type: {exception.GetType().FullName}: {exception.Message}");

        OkResponse response = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/raw-ok", cancellationToken);
        Assert.IsNotNull(response);
    }).AsTask();

    [TestMethod]
    public Task TransportIoFailure_DoesNotTerminateProductionWorker() =>
        ConnectionFailureDoesNotTerminateProductionWorkerAsync(new IOException("Simulated connection I/O failure."));

    [TestMethod]
    public Task TransportSocketFailure_DoesNotTerminateProductionWorker() =>
        ConnectionFailureDoesNotTerminateProductionWorkerAsync(new SocketException((int)SocketError.ConnectionReset));

    [TestMethod]
    public Task TransportAuthenticationFailure_DoesNotTerminateProductionWorker() =>
        ConnectionFailureDoesNotTerminateProductionWorkerAsync(new AuthenticationException("Simulated TLS authentication failure."));

    private Task ConnectionFailureDoesNotTerminateProductionWorkerAsync(Exception connectionFailure) => RunAsync(
        async (context, cancellationToken) =>
        {
            _ = await Assert.ThrowsAsync<Exception>(async () =>
            {
                _ = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/raw-ok", cancellationToken);
            });

            OkResponse response = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/raw-ok", cancellationToken);
            Assert.IsNotNull(response);
        },
        configuration: new IpcTestRunConfiguration
        {
            ConfigureServerServices = services =>
                services.Replace(ServiceDescriptor.Singleton<ITransportSecurityService>(
                    new FailOnceTransportSecurityService(connectionFailure))),
        }).AsTask();

    private sealed class FailOnceTransportSecurityService(Exception failure) : ITransportSecurityService
    {
        private readonly NoTransportSecurityService _inner = new();
        private int _failNextConnection = 1;

        public Task<Stream> OpenSecureStreamAsync(Stream innerStream, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _failNextConnection, 0) != 0)
            {
                return Task.FromException<Stream>(failure);
            }

            return _inner.OpenSecureStreamAsync(innerStream, cancellationToken);
        }
    }
}
