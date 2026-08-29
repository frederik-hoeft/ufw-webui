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
    [TestMethod]
    public async Task DuplexPair_CanExchangeSerializerFrames()
    {
        (Stream client, Stream server) = DuplexStreamPair.Create();
        await using (client)
        await using (server)
        {
            HybridMessageJsonSerializerContext context = HybridMessageJsonSerializerContext.CreateDefault();
            JsonMessageSerializer serializer = new(context);

            await using IMessage outbound = await serializer.SerializeAsync(
                id: "/api/v1/ping",
                method: "GET",
                payload: (object?)null,
                type: typeof(object),
                CancellationToken.None);

            Task serverTask = Task.Run(async () =>
            {
                ItpConnection serverItp = new(server);
                ItpFrame requestFrame = await serverItp.ReadAsync(CancellationToken.None);
                await using IMessage request = serializer.Decode(requestFrame.Payload);
                Assert.AreEqual("GET", request.Method);
                Assert.AreEqual("/api/v1/ping", request.Id);
                await using IMessage response = await serializer.SerializeAsync(
                    id: "200",
                    method: null,
                    payload: new Dictionary<string, bool> { ["ok"] = true },
                    type: typeof(Dictionary<string, bool>),
                    CancellationToken.None);
                await serverItp.WriteApplicationDataAsync(serializer.Encode(response), CancellationToken.None);
            });

            ItpConnection clientItp = new(client);
            await clientItp.WriteApplicationDataAsync(serializer.Encode(outbound), CancellationToken.None);
            ItpFrame responseFrame = await clientItp.ReadAsync(CancellationToken.None)
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5));
            await using IMessage inbound = serializer.Decode(responseFrame.Payload);
            Assert.AreEqual("200", inbound.Id);
            await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
