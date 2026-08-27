using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Shared.Serialization.Json;
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
                await using IMessage request = await serializer.ReadAsync(server, CancellationToken.None);
                Assert.AreEqual("GET", request.Method);
                Assert.AreEqual("/api/v1/ping", request.Id);
                await using IMessage response = await serializer.SerializeAsync(
                    id: "200",
                    method: null,
                    payload: new Dictionary<string, bool> { ["ok"] = true },
                    type: typeof(Dictionary<string, bool>),
                    CancellationToken.None);
                await serializer.WriteAsync(server, response, CancellationToken.None);
            });

            await serializer.WriteAsync(client, outbound, CancellationToken.None);
            await using IMessage inbound = await serializer.ReadAsync(client, CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual("200", inbound.Id);
            await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }
}
