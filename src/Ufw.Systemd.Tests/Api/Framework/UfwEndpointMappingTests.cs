using Moq;
using Ufw.Roslyn.Controllers;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Protocol;
using Ufw.Shared.Ipc.Serialization;
using Ufw.Systemd.Api.Framework;

namespace Ufw.Systemd.Tests.Api.Framework;

[TestClass]
public sealed class UfwEndpointMappingTests
{
    [TestMethod]
    public async Task InvokeAsync_RequestlessEndpoint_InvokesAndSerializesResponse()
    {
        Mock<IResponseMessage> serializedResponse = new();
        Mock<IMessageSerializer> serializer = new();
        serializer
            .Setup(value => value.SerializeResponseAsync(It.IsAny<TestResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serializedResponse.Object);
        Mock<IApiExceptionMapper> exceptionMapper = new();
        IServiceProvider services = CreateServices(serializer.Object, exceptionMapper.Object);
        TestResponse endpointResponse = new();
        int invocationCount = 0;
        UfwEndpointMapping<TestResponse> mapping = new("GET", "/test", 0, (_, _) =>
        {
            ++invocationCount;
            return ValueTask.FromResult(endpointResponse);
        });
        Mock<IRequestMessage> request = CreateRequest(hasPayload: false);

        IResponseMessage response = await mapping.InvokeAsync(services, request.Object, CancellationToken.None);

        Assert.AreSame(serializedResponse.Object, response);
        Assert.AreEqual(1, invocationCount);
        serializer.Verify(value => value.SerializeResponseAsync(endpointResponse, It.IsAny<CancellationToken>()), Times.Once);
        exceptionMapper.Verify(value => value.Map(It.IsAny<Exception>()), Times.Never);
    }

    [TestMethod]
    public async Task InvokeAsync_RequestEndpoint_BindsPayloadThenUsesSharedInvocationPath()
    {
        TestRequest requestPayload = new("payload");
        Mock<IMessageBlob> payload = new();
        payload.SetupGet(value => value.HasPayload).Returns(true);
        payload.Setup(value => value.ReadAsync<TestRequest>(It.IsAny<CancellationToken>())).ReturnsAsync(requestPayload);
        Mock<IRequestMessage> request = new();
        request.SetupGet(value => value.Payload).Returns(payload.Object);
        Mock<IResponseMessage> serializedResponse = new();
        Mock<IMessageSerializer> serializer = new();
        serializer
            .Setup(value => value.SerializeResponseAsync(It.IsAny<TestResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serializedResponse.Object);
        Mock<IApiExceptionMapper> exceptionMapper = new();
        IServiceProvider services = CreateServices(serializer.Object, exceptionMapper.Object);
        TestRequest? invokedWith = null;
        UfwEndpointMapping<TestRequest, TestResponse> mapping = new("POST", "/test", 0, (_, input, _) =>
        {
            invokedWith = input;
            return ValueTask.FromResult(new TestResponse());
        });

        IResponseMessage response = await mapping.InvokeAsync(services, request.Object, CancellationToken.None);

        Assert.AreSame(serializedResponse.Object, response);
        Assert.AreSame(requestPayload, invokedWith);
        exceptionMapper.Verify(value => value.Map(It.IsAny<Exception>()), Times.Never);
    }

    [TestMethod]
    public async Task InvokeAsync_MissingRequiredPayload_ReturnsBadRequestWithoutInvokingEndpoint()
    {
        Mock<IResponseMessage> badRequestResponse = new();
        Mock<IMessageSerializer> serializer = new();
        serializer
            .Setup(value => value.SerializeResponseAsync(It.IsAny<BadRequestResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(badRequestResponse.Object);
        Mock<IApiExceptionMapper> exceptionMapper = new();
        IServiceProvider services = CreateServices(serializer.Object, exceptionMapper.Object);
        bool invoked = false;
        UfwEndpointMapping<TestRequest, TestResponse> mapping = new("POST", "/test", 0, (_, _, _) =>
        {
            invoked = true;
            return ValueTask.FromResult(new TestResponse());
        });
        Mock<IRequestMessage> request = CreateRequest(hasPayload: false);

        IResponseMessage response = await mapping.InvokeAsync(services, request.Object, CancellationToken.None);

        Assert.AreSame(badRequestResponse.Object, response);
        Assert.IsFalse(invoked);
        serializer.Verify(value => value.SerializeResponseAsync(It.Is<BadRequestResponse>(badRequest => badRequest.Message == "This endpoint requires a request payload."), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task InvokeAsync_InvalidPayload_ReturnsBadRequestWithoutExceptionMapping()
    {
        const string errorMessage = "payload is invalid";
        Mock<IMessageBlob> payload = new();
        payload.SetupGet(value => value.HasPayload).Returns(true);
        payload
            .Setup(value => value.ReadAsync<TestRequest>(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromException<TestRequest?>(new ApplicationProtocolException(ApplicationProtocolError.PayloadDeserializeFailed, errorMessage)));
        Mock<IRequestMessage> request = new();
        request.SetupGet(value => value.Payload).Returns(payload.Object);
        Mock<IResponseMessage> badRequestResponse = new();
        Mock<IMessageSerializer> serializer = new();
        serializer
            .Setup(value => value.SerializeResponseAsync(It.IsAny<BadRequestResponse>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(badRequestResponse.Object);
        Mock<IApiExceptionMapper> exceptionMapper = new();
        IServiceProvider services = CreateServices(serializer.Object, exceptionMapper.Object);
        bool invoked = false;
        UfwEndpointMapping<TestRequest, TestResponse> mapping = new("POST", "/test", 0, (_, _, _) =>
        {
            invoked = true;
            return ValueTask.FromResult(new TestResponse());
        });

        IResponseMessage response = await mapping.InvokeAsync(services, request.Object, CancellationToken.None);

        Assert.AreSame(badRequestResponse.Object, response);
        Assert.IsFalse(invoked);
        serializer.Verify(value => value.SerializeResponseAsync(It.Is<BadRequestResponse>(badRequest => badRequest.Message == errorMessage), It.IsAny<CancellationToken>()), Times.Once);
        exceptionMapper.Verify(value => value.Map(It.IsAny<Exception>()), Times.Never);
    }

    [TestMethod]
    public async Task InvokeAsync_EndpointFailure_MapsAndSerializesExceptionOnce()
    {
        InvalidOperationException failure = new("boom");
        InternalServerErrorResponse mappedFailure = new("mapped");
        Mock<IResponseMessage> serializedResponse = new();
        Mock<IMessageSerializer> serializer = new();
        serializer
            .Setup(value => value.SerializeResponseAsync(mappedFailure, It.IsAny<CancellationToken>()))
            .ReturnsAsync(serializedResponse.Object);
        Mock<IApiExceptionMapper> exceptionMapper = new();
        exceptionMapper.Setup(value => value.Map(failure)).Returns(mappedFailure);
        IServiceProvider services = CreateServices(serializer.Object, exceptionMapper.Object);
        UfwEndpointMapping<TestResponse> mapping = new("GET", "/test", 0, (_, _) => ValueTask.FromException<TestResponse>(failure));
        Mock<IRequestMessage> request = CreateRequest(hasPayload: false);

        IResponseMessage response = await mapping.InvokeAsync(services, request.Object, CancellationToken.None);

        Assert.AreSame(serializedResponse.Object, response);
        exceptionMapper.Verify(value => value.Map(failure), Times.Once);
        serializer.Verify(value => value.SerializeResponseAsync(mappedFailure, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task InvokeAsync_CallerCancellation_PropagatesWithoutExceptionMapping()
    {
        Mock<IMessageSerializer> serializer = new();
        Mock<IApiExceptionMapper> exceptionMapper = new();
        IServiceProvider services = CreateServices(serializer.Object, exceptionMapper.Object);
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        UfwEndpointMapping<TestResponse> mapping = new("GET", "/test", 0, (_, token) => ValueTask.FromCanceled<TestResponse>(token));
        Mock<IRequestMessage> request = CreateRequest(hasPayload: false);

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await mapping.InvokeAsync(services, request.Object, cancellation.Token));

        exceptionMapper.Verify(value => value.Map(It.IsAny<Exception>()), Times.Never);
        serializer.Verify(value => value.SerializeResponseAsync(It.IsAny<TestResponse>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IRequestMessage> CreateRequest(bool hasPayload)
    {
        Mock<IMessageBlob> payload = new();
        payload.SetupGet(value => value.HasPayload).Returns(hasPayload);
        Mock<IRequestMessage> request = new();
        request.SetupGet(value => value.Payload).Returns(payload.Object);
        return request;
    }

    private static IServiceProvider CreateServices(IMessageSerializer serializer, IApiExceptionMapper exceptionMapper) =>
        new TestServiceProvider(new Dictionary<Type, object>
        {
            [typeof(IMessageSerializer)] = serializer,
            [typeof(IApiExceptionMapper)] = exceptionMapper,
        });

    private sealed class TestServiceProvider(IReadOnlyDictionary<Type, object> services) : IServiceProvider
    {
        public object? GetService(Type serviceType) => services.GetValueOrDefault(serviceType);
    }

    private sealed record TestRequest(string Value);

    private sealed record TestResponse : IIdentifiable
    {
        public string Id => "test";

        public string? Method => null;
    }
}
