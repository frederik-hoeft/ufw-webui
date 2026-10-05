using System.Runtime.Serialization;
using System.Security.Authentication;
using Ufw.Ipc.Client;
using Ufw.Ipc.Client.Configuration;
using Ufw.Ipc.Client.Handlers;
using Ufw.Ipc.Client.Transport;
using Ufw.Roslyn.Controllers;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Protocol;
using Ufw.Shared.Ipc.Serialization;

namespace Ufw.Ipc.Tests;

[TestClass]
public sealed class UfwClientInvalidResponseTests
{
    [TestMethod]
    public async Task TrySendAsync_ResponseHandlerSerializationFailureUsesInvalidResponseContractAsync()
    {
        using StubRequest request = new();
        using StubResponse response = new(StatusCode: 200, PayloadType: "unexpected");
        UfwClient client = CreateClient(new StubMessageExchange(response), request, [new DataResponseHandler()]);

        UfwIpcInvalidResponseException exception = await Assert.ThrowsExactlyAsync<UfwIpcInvalidResponseException>(
            () => client.TrySendAsync<OkResponse>(RequestMethod.Get, "/test", CancellationToken.None));

        Assert.IsInstanceOfType<SerializationException>(exception.InnerException);
    }

    [TestMethod]
    public async Task TrySendAsync_ResponseProtocolFailureUsesInvalidResponseContractAsync()
    {
        using StubRequest request = new();
        ApplicationProtocolException protocolException = new(ApplicationProtocolError.InvalidJson, "Malformed response.");
        UfwClient client = CreateClient(new ThrowingMessageExchange(protocolException), request, [new DataResponseHandler()]);

        UfwIpcInvalidResponseException exception = await Assert.ThrowsExactlyAsync<UfwIpcInvalidResponseException>(
            () => client.TrySendAsync<OkResponse>(RequestMethod.Get, "/test", CancellationToken.None));

        Assert.AreSame(protocolException, exception.InnerException);
    }

    private static UfwClient CreateClient(IClientMessageExchange exchange, IRequestMessage request, IEnumerable<IResponseMessageHandler> handlers)
    {
        UfwClientOptions options = new(
            ServerName: "test",
            PipeName: "test",
            TlsEnabled: false,
            TlsServerName: null,
            SslProtocols.None,
            IoTimeout: TimeSpan.FromSeconds(1),
            RequestTimeout: Timeout.InfiniteTimeSpan);
        return new UfwClient(new StubMessageSerializer(request), exchange, handlers, options);
    }

    private sealed class StubMessageSerializer(IRequestMessage request) : IMessageSerializer
    {
        public ValueTask<IRequestMessage> SerializeRequestAsync(string route, string method, CancellationToken cancellationToken) => ValueTask.FromResult(request);

        public ValueTask<IRequestMessage> SerializeRequestAsync<T>(string route, string method, T payload, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<IRequestMessage> SerializeRequestAsync(string route, string method, object? payload, Type type, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<IResponseMessage> SerializeResponseAsync<T>(T payload, CancellationToken cancellationToken) where T : IIdentifiable =>
            throw new NotSupportedException();

        public byte[] Encode(IMessage message) => throw new NotSupportedException();

        public IMessage Decode(ReadOnlyMemory<byte> buffer) => throw new NotSupportedException();
    }

    private sealed class StubMessageExchange(IResponseMessage response) : IClientMessageExchange
    {
        public ValueTask<IResponseMessage> ExchangeAsync(IRequestMessage request, CancellationToken cancellationToken = default) => ValueTask.FromResult(response);
    }

    private sealed class ThrowingMessageExchange(Exception exception) : IClientMessageExchange
    {
        public ValueTask<IResponseMessage> ExchangeAsync(IRequestMessage request, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<IResponseMessage>(exception);
    }

    private sealed class StubRequest : IRequestMessage
    {
        public ApplicationMessageKind Kind => ApplicationMessageKind.Request;
        public int ProtocolVersion => ApplicationProtocolVersion.CURRENT;
        public string PayloadType => ApplicationPayloadTypes.EMPTY;
        public IMessageBlob Payload { get; } = new StubMessageBlob();
        public string Method => RequestMethod.Get.ToString();
        public string Route => "/test";
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class StubResponse(int StatusCode, string PayloadType) : IResponseMessage
    {
        public ApplicationMessageKind Kind => ApplicationMessageKind.Response;
        public int ProtocolVersion => ApplicationProtocolVersion.CURRENT;
        public string PayloadType { get; } = PayloadType;
        public IMessageBlob Payload { get; } = new StubMessageBlob();
        public int StatusCode { get; } = StatusCode;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class StubMessageBlob : IMessageBlob
    {
        public bool HasPayload => false;
        public ReadOnlyMemory<byte> Utf8 => ReadOnlyMemory<byte>.Empty;
        public ValueTask<TResult?> ReadAsync<TResult>(CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
