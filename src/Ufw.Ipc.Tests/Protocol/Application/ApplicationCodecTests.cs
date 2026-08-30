using System.Text;
using System.Text.Json;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Protocol;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Shared.Serialization.Json;
using Ufw.Ipc.Tests.Adapter.Serialization;

namespace Ufw.Ipc.Tests.Protocol.Application;

[TestClass]
public sealed class ApplicationCodecTests
{
    private static JsonMessageSerializer CreateSerializer() =>
        new(HybridMessageJsonSerializerContext.CreateDefault());

    [TestMethod]
    public async Task EncodeDecode_GetRequest_RoundTrips()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        await using IRequestMessage original = await serializer.SerializeRequestAsync(
            "/api/v1/ping",
            "GET",
            CancellationToken.None);

        IRequestMessage decoded = RequireRequest(serializer.Decode(serializer.Encode(original)));
        Assert.AreEqual(ApplicationMessageKind.Request, decoded.Kind);
        Assert.AreEqual("GET", decoded.Method);
        Assert.AreEqual("/api/v1/ping", decoded.Route);
        Assert.AreEqual(ApplicationPayloadTypes.Empty, decoded.PayloadType);
    }

    [TestMethod]
    public async Task EncodeDecode_ValidationError_KeepsDiscriminator()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        ModelValidationErrorResponse payload = new(
        [
            new ModelValidationError("port", "out of range"),
        ]);
        await using IResponseMessage original = await serializer.SerializeResponseAsync(payload, CancellationToken.None);

        IResponseMessage decoded = RequireResponse(serializer.Decode(serializer.Encode(original)));
        Assert.AreEqual(ApplicationMessageKind.Response, decoded.Kind);
        Assert.AreEqual(400, decoded.StatusCode);
        Assert.AreEqual(ApplicationPayloadTypes.ValidationError, decoded.PayloadType);

        ModelValidationErrorResponse? body = await decoded.Payload.ReadAsync<ModelValidationErrorResponse>(CancellationToken.None);
        Assert.IsNotNull(body);
        Assert.HasCount(1, body.Errors);
        Assert.AreEqual("port", body.Errors[0].PropertyName);
    }

    [TestMethod]
    public async Task EncodeDecode_GenericBadRequest_IsErrorPayloadType()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        await using IResponseMessage original = await serializer.SerializeResponseAsync(new BadRequestResponse("nope"), CancellationToken.None);
        IResponseMessage decoded = RequireResponse(serializer.Decode(serializer.Encode(original)));

        Assert.AreEqual(400, decoded.StatusCode);
        Assert.AreEqual(ApplicationPayloadTypes.Error, decoded.PayloadType);
        Assert.AreNotEqual(ApplicationPayloadTypes.ValidationError, decoded.PayloadType);
    }

    [TestMethod]
    public void Decode_EmptyBuffer_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(
            () => serializer.Decode(ReadOnlyMemory<byte>.Empty));
        Assert.AreEqual(ApplicationProtocolError.EmptyDocument, exception.Error);
    }

    [TestMethod]
    public void Decode_EmptyObject_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(
            () => serializer.Decode("{}"u8.ToArray()));
        Assert.IsTrue(
            exception.Error is ApplicationProtocolError.InvalidJson or ApplicationProtocolError.MissingRequiredField);
    }

    [TestMethod]
    public void Decode_JsonNull_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode("null"u8.ToArray()));
    }

    [TestMethod]
    public void Decode_WrongVersion_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":2,"kind":"request","method":"GET","route":"/x","payloadType":"empty"}
            """u8.ToArray();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode(json));
        Assert.AreEqual(ApplicationProtocolError.VersionMismatch, exception.Error);
    }

    [TestMethod]
    public void Decode_UnknownKind_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"event","method":"GET","route":"/x","payloadType":"empty"}
            """u8.ToArray();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode(json));
        Assert.AreEqual(ApplicationProtocolError.InvalidKind, exception.Error);
    }

    [TestMethod]
    public void Decode_UnknownPayloadType_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"response","status":400,"payloadType":"mystery","payload":{"message":"x"}}
            """u8.ToArray();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode(json));
        Assert.AreEqual(ApplicationProtocolError.UnknownPayloadType, exception.Error);
    }


    [TestMethod]
    public void Decode_RequestWithErrorRepresentation_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"request","method":"POST","route":"/x","payloadType":"error","payload":{"message":"x"}}
            """u8.ToArray();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode(json));
        Assert.AreEqual(ApplicationProtocolError.PayloadTypeMismatch, exception.Error);
    }

    [TestMethod]
    public void Decode_RequestWithValidationErrorRepresentation_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"request","method":"POST","route":"/x","payloadType":"validation-error","payload":{"errors":[]}}
            """u8.ToArray();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode(json));
        Assert.AreEqual(ApplicationProtocolError.PayloadTypeMismatch, exception.Error);
    }

    [TestMethod]
    public void Decode_ValidationErrorRepresentationWithNon400Status_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"response","status":422,"payloadType":"validation-error","payload":{"errors":[]}}
            """u8.ToArray();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode(json));
        Assert.AreEqual(ApplicationProtocolError.PayloadTypeMismatch, exception.Error);
    }

    [TestMethod]
    public void Decode_RequestMissingMethod_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"request","route":"/x","payloadType":"empty"}
            """u8.ToArray();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode(json));
        Assert.AreEqual(ApplicationProtocolError.MissingRequiredField, exception.Error);
    }

    [TestMethod]
    public void Decode_RequestMissingRoute_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"request","method":"GET","payloadType":"empty"}
            """u8.ToArray();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode(json));
        Assert.AreEqual(ApplicationProtocolError.MissingRequiredField, exception.Error);
    }

    [TestMethod]
    public void Decode_RequestWithStatus_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"request","method":"GET","route":"/x","status":200,"payloadType":"empty"}
            """u8.ToArray();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode(json));
        Assert.AreEqual(ApplicationProtocolError.UnexpectedField, exception.Error);
    }

    [TestMethod]
    public void Decode_ResponseWithMethod_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"response","status":200,"method":"GET","payloadType":"empty"}
            """u8.ToArray();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode(json));
        Assert.AreEqual(ApplicationProtocolError.UnexpectedField, exception.Error);
    }

    [TestMethod]
    public async Task EncodeDecode_ExplicitJsonNull_IsPresentDataPayload()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        await using IRequestMessage original = await serializer.SerializeRequestAsync<object?>(
            "/api/v1/null",
            "POST",
            payload: null,
            CancellationToken.None);

        Assert.AreEqual(ApplicationPayloadTypes.Data, original.PayloadType);
        Assert.IsTrue(original.Payload.HasPayload);

        byte[] encoded = serializer.Encode(original);
        using (JsonDocument document = JsonDocument.Parse(encoded))
        {
            Assert.AreEqual(JsonValueKind.Null, document.RootElement.GetProperty("payload").ValueKind);
        }

        await using IRequestMessage decoded = RequireRequest(serializer.Decode(encoded));
        Assert.AreEqual(ApplicationPayloadTypes.Data, decoded.PayloadType);
        Assert.IsTrue(decoded.Payload.HasPayload);
        object? value = await decoded.Payload.ReadAsync<object?>(CancellationToken.None);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void Decode_EmptyPayloadTypeWithJsonNull_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"request","method":"GET","route":"/x","payloadType":"empty","payload":null}
            """u8.ToArray();

        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode(json));
        Assert.AreEqual(ApplicationProtocolError.PayloadTypeMismatch, exception.Error);
    }

    [TestMethod]
    public async Task Decode_DataPayloadTypeWithJsonNull_IsPresent()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"request","method":"POST","route":"/x","payloadType":"data","payload":null}
            """u8.ToArray();

        await using IRequestMessage decoded = RequireRequest(serializer.Decode(json));
        Assert.IsTrue(decoded.Payload.HasPayload);
        string? value = await decoded.Payload.ReadAsync<string?>(CancellationToken.None);
        Assert.IsNull(value);

        ApplicationProtocolException exception = await Assert.ThrowsExactlyAsync<ApplicationProtocolException>(async () =>
            await decoded.Payload.ReadAsync<int>(CancellationToken.None));
        Assert.AreEqual(ApplicationProtocolError.PayloadDeserializeFailed, exception.Error);
    }

    [TestMethod]
    public async Task PayloadRead_Absent_ThrowsInsteadOfReturningDefaultValue()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        await using IRequestMessage request = await serializer.SerializeRequestAsync(
            "/api/v1/empty",
            "GET",
            CancellationToken.None);

        Assert.IsFalse(request.Payload.HasPayload);
        ApplicationProtocolException exception = await Assert.ThrowsExactlyAsync<ApplicationProtocolException>(async () =>
            await request.Payload.ReadAsync<int>(CancellationToken.None));
        Assert.AreEqual(ApplicationProtocolError.MissingPayload, exception.Error);
    }

    [TestMethod]
    public void Decode_EmptyPayloadTypeWithBody_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"response","status":200,"payloadType":"empty","payload":{"ok":true}}
            """u8.ToArray();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode(json));
        Assert.AreEqual(ApplicationProtocolError.PayloadTypeMismatch, exception.Error);
    }

    [TestMethod]
    public void Decode_DataPayloadTypeWithoutBody_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"response","status":200,"payloadType":"data"}
            """u8.ToArray();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode(json));
        Assert.AreEqual(ApplicationProtocolError.PayloadTypeMismatch, exception.Error);
    }

    [TestMethod]
    public void Decode_SuccessResponseWithErrorRepresentation_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"response","status":200,"payloadType":"error","payload":{"message":"x"}}
            """u8.ToArray();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode(json));
        Assert.AreEqual(ApplicationProtocolError.PayloadTypeMismatch, exception.Error);
    }

    [TestMethod]
    public void Decode_ErrorResponseWithDataRepresentation_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"response","status":404,"payloadType":"data","payload":{"message":"x"}}
            """u8.ToArray();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode(json));
        Assert.AreEqual(ApplicationProtocolError.PayloadTypeMismatch, exception.Error);
    }

    [TestMethod]
    public void Decode_ErrorRepresentationWithNonObjectPayload_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"response","status":400,"payloadType":"error","payload":["x"]}
            """u8.ToArray();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode(json));
        Assert.AreEqual(ApplicationProtocolError.PayloadTypeMismatch, exception.Error);
    }

    [TestMethod]
    public void Decode_ValidationErrorWithoutErrorsArray_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"response","status":400,"payloadType":"validation-error","payload":{"message":"bad"}}
            """u8.ToArray();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode(json));
        Assert.AreEqual(ApplicationProtocolError.PayloadTypeMismatch, exception.Error);
    }

    [TestMethod]
    public void Decode_ResponseMissingStatus_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"response","payloadType":"empty"}
            """u8.ToArray();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode(json));
        Assert.AreEqual(ApplicationProtocolError.InvalidStatus, exception.Error);
    }

    [TestMethod]
    public void Decode_InvalidStatus_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"response","status":99,"payloadType":"empty"}
            """u8.ToArray();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(() => serializer.Decode(json));
        Assert.AreEqual(ApplicationProtocolError.InvalidStatus, exception.Error);
    }

    [TestMethod]
    public void Decode_NotJson_IsRejected()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        ApplicationProtocolException exception = Assert.ThrowsExactly<ApplicationProtocolException>(
            () => serializer.Decode(Encoding.UTF8.GetBytes("{not-json")));
        Assert.AreEqual(ApplicationProtocolError.InvalidJson, exception.Error);
    }

    [TestMethod]
    public async Task PayloadRead_InvalidDto_DoesNotReturnDefaultObject()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        byte[] json = """
            {"protocolVersion":1,"kind":"response","status":200,"payloadType":"data","payload":[1,2,3]}
            """u8.ToArray();
        IMessage decoded = serializer.Decode(json);
        await Assert.ThrowsExactlyAsync<ApplicationProtocolException>(async () =>
            await decoded.Payload.ReadAsync<ModelValidationErrorResponse>(CancellationToken.None));
    }

    [TestMethod]
    public async Task Encode_ProducesCamelCaseKindAndPayloadType()
    {
        JsonMessageSerializer serializer = CreateSerializer();
        await using IResponseMessage message = await serializer.SerializeResponseAsync(new OkResponse(), CancellationToken.None);
        string json = Encoding.UTF8.GetString(serializer.Encode(message));
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.AreEqual(1, document.RootElement.GetProperty("protocolVersion").GetInt32());
        Assert.AreEqual("response", document.RootElement.GetProperty("kind").GetString());
        Assert.AreEqual(200, document.RootElement.GetProperty("status").GetInt32());
        Assert.AreEqual("empty", document.RootElement.GetProperty("payloadType").GetString());
        Assert.IsFalse(document.RootElement.TryGetProperty("payload", out _));
        Assert.IsFalse(document.RootElement.TryGetProperty("method", out _));
        Assert.IsFalse(document.RootElement.TryGetProperty("route", out _));
    }

    private static IRequestMessage RequireRequest(IMessage message)
    {
        Assert.IsTrue(message is IRequestMessage);
        return (IRequestMessage)message;
    }

    private static IResponseMessage RequireResponse(IMessage message)
    {
        Assert.IsTrue(message is IResponseMessage);
        return (IResponseMessage)message;
    }
}
