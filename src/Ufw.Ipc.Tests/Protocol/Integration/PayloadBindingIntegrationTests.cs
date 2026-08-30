using System.Text;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Protocol;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Tests.Adapter;
using Ufw.Ipc.Tests.Adapter.Endpoints;

namespace Ufw.Ipc.Tests.Protocol.Integration;

[TestClass]
public sealed class PayloadBindingIntegrationTests : IpcProtocolTestBase
{
    private const string Route = "/api/v1/bind";

    [TestMethod]
    public Task RequiredClassBody_Absent_DoesNotInvokeEndpoint() =>
        AssertRejectedBeforeInvocationAsync<ClassBody>(ApplicationPayloadTypes.Empty);

    [TestMethod]
    public Task RequiredStructBody_Absent_DoesNotInvokeEndpoint() =>
        AssertRejectedBeforeInvocationAsync<StructBody>(ApplicationPayloadTypes.Empty);

    [TestMethod]
    public Task RequiredClassBody_JsonNull_DoesNotInvokeEndpoint() =>
        AssertRejectedBeforeInvocationAsync<ClassBody>(ApplicationPayloadTypes.Data, "null");

    [TestMethod]
    public Task RequiredStructBody_JsonNull_DoesNotInvokeEndpoint() =>
        AssertRejectedBeforeInvocationAsync<StructBody>(ApplicationPayloadTypes.Data, "null");

    [TestMethod]
    public Task MalformedJson_DoesNotInvokeEndpoint()
    {
        int invocationCount = 0;
        return RunAsync(
            configureEndpoints: endpoints => endpoints.MapPost<ClassBody, OkResponse>(
                Route,
                (request, _) =>
                {
                    Interlocked.Increment(ref invocationCount);
                    return ValueTask.FromResult(new OkResponse());
                }),
            actAsync: async (context, cancellationToken) =>
            {
                ReadOnlyMemory<byte> malformed = Encoding.UTF8.GetBytes(
                    $"{{\"protocolVersion\":1,\"kind\":\"request\",\"method\":\"POST\",\"route\":\"{Route}\",\"payloadType\":\"data\",\"payload\":{{");
                await using IResponseMessage response = await context.ExchangeApplicationBytesAsync(malformed, cancellationToken);

                Assert.AreEqual(400, response.StatusCode);
                Assert.AreEqual(ApplicationPayloadTypes.Error, response.PayloadType);
                Assert.AreEqual(0, Volatile.Read(ref invocationCount));
            }).AsTask();
    }

    [TestMethod]
    public Task RequiredClassBody_WrongShape_DoesNotInvokeEndpoint() =>
        AssertRejectedBeforeInvocationAsync<ClassBody>(ApplicationPayloadTypes.Data, "17");

    [TestMethod]
    public Task RequiredStructBody_WrongShape_DoesNotInvokeEndpoint() =>
        AssertRejectedBeforeInvocationAsync<StructBody>(ApplicationPayloadTypes.Data, "\"wrong-shape\"");

    [TestMethod]
    public Task EmptyObject_ClassBody_IsBoundAccordingToDtoSemantics() =>
        AssertAcceptedAsync<ClassBody>("{}", static request => Assert.IsNull(request.Message));

    [TestMethod]
    public Task EmptyObject_StructBody_IsPresentDefaultValue() =>
        AssertAcceptedAsync<StructBody>("{}", static request => Assert.AreEqual(0, request.Value));

    [TestMethod]
    public Task NumericZero_IsNotConfusedWithAbsence() =>
        AssertAcceptedAsync<int>("0", static request => Assert.AreEqual(0, request));

    [TestMethod]
    public Task False_IsNotConfusedWithAbsence() =>
        AssertAcceptedAsync<bool>("false", static request => Assert.IsFalse(request));

    [TestMethod]
    public Task NoBodyEndpoint_AbsentPayload_InvokesEndpoint()
    {
        int invocationCount = 0;
        return RunAsync(
            configureEndpoints: endpoints => endpoints.MapGet(
                Route,
                _ =>
                {
                    Interlocked.Increment(ref invocationCount);
                    return ValueTask.FromResult(new OkResponse());
                }),
            actAsync: async (context, cancellationToken) =>
            {
                OkResponse response = await context.SendAsync<OkResponse>(RequestMethod.Get, Route, cancellationToken);
                Assert.IsNotNull(response);
                Assert.AreEqual(1, Volatile.Read(ref invocationCount));
            }).AsTask();
    }

    [TestMethod]
    public Task NoBodyEndpoint_PresentPayload_IsRejectedBeforeInvocation()
    {
        int invocationCount = 0;
        return RunAsync(
            configureEndpoints: endpoints => endpoints.MapGet(
                Route,
                _ =>
                {
                    Interlocked.Increment(ref invocationCount);
                    return ValueTask.FromResult(new OkResponse());
                }),
            actAsync: async (context, cancellationToken) =>
            {
                await using IResponseMessage response = await context.ExchangeApplicationBytesAsync(
                    BuildRequest("GET", ApplicationPayloadTypes.Data, "{}"),
                    cancellationToken);

                Assert.AreEqual(400, response.StatusCode);
                Assert.AreEqual(ApplicationPayloadTypes.Error, response.PayloadType);
                Assert.AreEqual(0, Volatile.Read(ref invocationCount));
            }).AsTask();
    }

    private Task AssertRejectedBeforeInvocationAsync<TRequest>(string payloadType, string? payloadJson = null)
    {
        int invocationCount = 0;
        return RunAsync(
            configureEndpoints: endpoints => endpoints.MapPost<TRequest, OkResponse>(
                Route,
                (request, _) =>
                {
                    Interlocked.Increment(ref invocationCount);
                    return ValueTask.FromResult(new OkResponse());
                }),
            actAsync: async (context, cancellationToken) =>
            {
                await using IResponseMessage response = await context.ExchangeApplicationBytesAsync(
                    BuildRequest("POST", payloadType, payloadJson),
                    cancellationToken);

                Assert.AreEqual(400, response.StatusCode);
                Assert.AreEqual(ApplicationPayloadTypes.Error, response.PayloadType);
                Assert.AreEqual(0, Volatile.Read(ref invocationCount));
            }).AsTask();
    }

    private Task AssertAcceptedAsync<TRequest>(string payloadJson, Action<TRequest> assertRequest)
    {
        int invocationCount = 0;
        TRequest? received = default;
        return RunAsync(
            configureEndpoints: endpoints => endpoints.MapPost<TRequest, OkResponse>(
                Route,
                (request, _) =>
                {
                    received = request;
                    Interlocked.Increment(ref invocationCount);
                    return ValueTask.FromResult(new OkResponse());
                }),
            actAsync: async (context, cancellationToken) =>
            {
                await using IResponseMessage response = await context.ExchangeApplicationBytesAsync(
                    BuildRequest("POST", ApplicationPayloadTypes.Data, payloadJson),
                    cancellationToken);

                Assert.AreEqual(200, response.StatusCode);
                Assert.AreEqual(1, Volatile.Read(ref invocationCount));
                assertRequest(received!);
            }).AsTask();
    }

    private static ReadOnlyMemory<byte> BuildRequest(string method, string payloadType, string? payloadJson = null)
    {
        string payload = payloadJson is null ? string.Empty : $",\"payload\":{payloadJson}";
        return Encoding.UTF8.GetBytes(
            $"{{\"protocolVersion\":1,\"kind\":\"request\",\"method\":\"{method}\",\"route\":\"{Route}\",\"payloadType\":\"{payloadType}\"{payload}}}");
    }
}

file sealed record ClassBody(string? Message = null);

file readonly record struct StructBody(int Value);
