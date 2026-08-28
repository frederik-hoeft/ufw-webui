using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Protocol;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Tests.Adapter;
using Ufw.Ipc.Tests.Adapter.Endpoints;

namespace Ufw.Ipc.Tests.Protocol.Integration;

[TestClass]
public sealed class ApplicationProtocolIntegrationTests : IpcProtocolTestBase
{
    protected override ValueTask ConfigureEndpointsAsync(ITestEndpointMapBuilder endpoints, CancellationToken cancellationToken)
    {
        endpoints.MapGet("/api/v1/ping", static _ => ValueTask.FromResult(new OkResponse()));
        endpoints.MapPost<EchoRequest, EchoResponse>(
            "/api/v1/echo",
            static (request, _) => ValueTask.FromResult(new EchoResponse(request.Message)));
        endpoints.MapPost<EchoRequest, OkResponse>(
            "/api/v1/validate",
            static (request, _) =>
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    throw new InvalidOperationException("unreachable");
                }

                return ValueTask.FromResult(new OkResponse());
            });
        return ValueTask.CompletedTask;
    }

    [TestMethod]
    public Task TypedPing_StillReturnsOk() => RunAsync(async (context, cancellationToken) =>
    {
        OkResponse response = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/ping", cancellationToken);
        Assert.IsNotNull(response);
    }).AsTask();

    [TestMethod]
    public Task TypedEcho_StillRoundTrips() => RunAsync(async (context, cancellationToken) =>
    {
        EchoResponse response = await context.SendAsync<EchoRequest, EchoResponse>(
            RequestMethod.Post,
            "/api/v1/echo",
            new EchoRequest("itp"),
            cancellationToken);
        Assert.AreEqual("itp", response.Message);
    }).AsTask();

    [TestMethod]
    public Task UnknownRoute_StillReturns404ErrorPayload() => RunAsync(async (context, cancellationToken) =>
    {
        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            _ = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/missing", cancellationToken);
        });
        StringAssert.Contains(exception.Message, "404");
    }).AsTask();

    [TestMethod]
    public Task Generic400AndValidation400_AreDistinctOnTheWire() => RunAsync(async (context, cancellationToken) =>
    {
        await using IMessage missingMethod = await context.MessageSerializer.SerializeAsync(
            id: "/api/v1/ping",
            method: null,
            payload: (object?)null,
            type: typeof(object),
            cancellationToken);
        await using IMessage generic = await context.ExchangeRawAsync(missingMethod, cancellationToken);
        Assert.AreEqual(400, generic.StatusCode);
        Assert.AreEqual(ApplicationPayloadTypes.Error, generic.PayloadType);

        await using IMessage validation = await context.MessageSerializer.SerializeAsync(
            new ModelValidationErrorResponse([new ModelValidationError("message", "required")]),
            cancellationToken);
        Assert.AreEqual(ApplicationPayloadTypes.ValidationError, validation.PayloadType);
        Assert.AreNotEqual(generic.PayloadType, validation.PayloadType);
    }).AsTask();

    [TestMethod]
    public Task RawExchange_OkHasEmptyPayloadType() => RunAsync(async (context, cancellationToken) =>
    {
        await using IMessage request = await context.MessageSerializer.SerializeAsync(
            "/api/v1/ping",
            RequestMethod.Get.ToString(),
            payload: (object?)null,
            typeof(object),
            cancellationToken);
        await using IMessage response = await context.ExchangeRawAsync(request, cancellationToken);
        Assert.AreEqual(ApplicationMessageKind.Response, response.Kind);
        Assert.AreEqual(200, response.StatusCode);
        Assert.AreEqual(ApplicationPayloadTypes.Empty, response.PayloadType);
        Assert.IsTrue(response.Payload.IsEmpty);
    }).AsTask();

    [TestMethod]
    public Task Cancellation_UnblocksClient() => RunAsync(async (context, cancellationToken) =>
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await cts.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            _ = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/ping", cts.Token);
        });
    }).AsTask();

    [TestMethod]
    public Task ValidationErrorResponse_IsNotTreatedAsGenericBadRequest() => RunAsync(
        configureEndpoints: static endpoints =>
        {
            endpoints.MapPost<EchoRequest, ModelValidationErrorResponse>(
                "/api/v1/reject",
                static (_, _) => ValueTask.FromResult(
                    new ModelValidationErrorResponse([new ModelValidationError("message", "required")])));
        },
        actAsync: async (context, cancellationToken) =>
        {
            InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            {
                _ = await context.SendAsync<EchoRequest, OkResponse>(
                    RequestMethod.Post,
                    "/api/v1/reject",
                    new EchoRequest("x"),
                    cancellationToken);
            });
            StringAssert.Contains(exception.Message, "message: required");
        }).AsTask();
}

file sealed record EchoRequest(string Message);

file sealed record EchoResponse(string Message) : OkResponseBase;
