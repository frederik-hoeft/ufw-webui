using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Shared.Serialization.Json;
using Ufw.Ipc.Shared.Transport.Itp;
using Ufw.Ipc.Tests.Adapter.Serialization;
using Ufw.Ipc.Tests.Adapter.Transport;

namespace Ufw.Ipc.Tests.Smoke;

/// <summary>
/// Low-level transport sanity checks for the in-process duplex stream used by the adapter.
/// </summary>
[TestClass]
public sealed class DiagnosticTransportTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task TestDuplexPair_CanExchangeSerializerFramesAsync()
    {
        (Stream client, Stream server) = DuplexStreamPair.Create();
        await using (client)
        await using (server)
        {
            HybridMessageJsonSerializerContext context = HybridMessageJsonSerializerContext.CreateDefault();
            JsonMessageSerializer serializer = new(context);

            await using IRequestMessage outbound = await serializer.SerializeRequestAsync(
                route: "/api/v1/ping",
                method: "GET",
                CancellationToken.None);

            Task serverTask = Task.Run(async () =>
            {
                ItpConnection serverItp = new(server);
                ItpFrame requestFrame = await serverItp.ReadAsync(CancellationToken.None);
                await using IMessage decodedRequest = serializer.Decode(requestFrame.Payload);
                Assert.IsTrue(decodedRequest is IRequestMessage);
                IRequestMessage request = (IRequestMessage)decodedRequest;
                Assert.AreEqual("GET", request.Method);
                Assert.AreEqual("/api/v1/ping", request.Route);
                await using IResponseMessage response = await serializer.SerializeResponseAsync(
                    new DiagnosticResponse(true),
                    CancellationToken.None);
                await serverItp.WriteApplicationDataAsync(serializer.Encode(response), CancellationToken.None);
            }, TestContext.CancellationToken);

            ItpConnection clientItp = new(client);
            await clientItp.WriteApplicationDataAsync(serializer.Encode(outbound), CancellationToken.None);
            ItpFrame responseFrame = await clientItp.ReadAsync(CancellationToken.None)
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
            await using IMessage decodedResponse = serializer.Decode(responseFrame.Payload);
            Assert.IsTrue(decodedResponse is IResponseMessage);
            IResponseMessage inbound = (IResponseMessage)decodedResponse;
            Assert.AreEqual(200, inbound.StatusCode);
            DiagnosticResponse? body = await inbound.Payload.ReadAsync<DiagnosticResponse>(CancellationToken.None);
            Assert.AreEqual(new DiagnosticResponse(true), body);
            await serverTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        }
    }
}

file sealed record DiagnosticResponse(bool Ok) : OkResponseBase;
