using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Protocol;
using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Json;

namespace Ufw.Ipc.Shared.Serialization.Json;

/// <summary>
/// Application-level JSON codec. It encodes and decodes complete application
/// documents and has no responsibility for stream framing or ITP transport semantics.
/// </summary>
public sealed class JsonMessageSerializer(AotJsonSerializerContext context) : IMessageSerializer
{
    [SuppressMessage("Reliability", CA2000_WARN_OBJECT_NOT_DISPOSED, Justification = CA2000_OWNERSHIP_TRANSFER)]
    public ValueTask<IRequestMessage> SerializeRequestAsync<T>(string route, string method, T payload, CancellationToken cancellationToken)
    {
        ValidateRequestMetadata(route, method, payload);
        BufferedJsonMessageBlob payloadBlob = BufferedJsonMessageBlob.CreateFrom(payload, context);
        IRequestMessage message = CreateRequestMessage(route, method, payload, payloadBlob);
        return ValueTask.FromResult(message);
    }

    [SuppressMessage("Reliability", CA2000_WARN_OBJECT_NOT_DISPOSED, Justification = CA2000_OWNERSHIP_TRANSFER)]
    public ValueTask<IRequestMessage> SerializeRequestAsync(string route, string method, object? payload, Type type, CancellationToken cancellationToken)
    {
        ValidateRequestMetadata(route, method, payload);
        BufferedJsonMessageBlob payloadBlob = BufferedJsonMessageBlob.CreateFrom(payload, type, context);
        IRequestMessage message = CreateRequestMessage(route, method, payload, payloadBlob);
        return ValueTask.FromResult(message);
    }

    [SuppressMessage("Reliability", CA2000_WARN_OBJECT_NOT_DISPOSED, Justification = CA2000_OWNERSHIP_TRANSFER)]
    public ValueTask<IResponseMessage> SerializeResponseAsync<T>(T payload, CancellationToken cancellationToken) where T : IIdentifiable
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload is not IResponsePayload responsePayload)
        {
            throw new ArgumentException("The payload does not define application response semantics.", nameof(payload));
        }

        BufferedJsonMessageBlob payloadBlob = BufferedJsonMessageBlob.CreateFrom(payload, context);
        IResponseMessage message = new ResponseMessage(
            ApplicationProtocolVersion.Current,
            (int)responsePayload.StatusCode,
            ResolveResponsePayloadType(responsePayload),
            payloadBlob);
        return ValueTask.FromResult(message);
    }

    public byte[] Encode(IMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ApplicationEnvelope envelope = ToEnvelope(message);
        return JsonSerializer.SerializeToUtf8Bytes(envelope, context.GetTypeInfo<ApplicationEnvelope>());
    }

    public IMessage Decode(ReadOnlyMemory<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            throw new ApplicationProtocolException(
                ApplicationProtocolError.EmptyDocument,
                "Application payload is empty.");
        }

        ApplicationEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize(buffer.Span, context.GetTypeInfo<ApplicationEnvelope>());
        }
        catch (JsonException ex)
        {
            throw new ApplicationProtocolException(
                ApplicationProtocolError.InvalidJson,
                "Application payload is not valid JSON.",
                ex);
        }

        if (envelope is null)
        {
            throw new ApplicationProtocolException(
                ApplicationProtocolError.EmptyDocument,
                "Application payload deserialized to null.");
        }

        return FromEnvelope(envelope);
    }

    private static void ValidateRequestMetadata<T>(string route, string method, T payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        if (payload is IResponsePayload)
        {
            throw new ArgumentException("A response payload cannot be serialized as an application request.", nameof(payload));
        }
    }

    private static IRequestMessage CreateRequestMessage(string route, string method, object? payload, IMessageBlob payloadBlob) =>
        new RequestMessage(
            ApplicationProtocolVersion.Current,
            method,
            route,
            ResolveRequestPayloadType(payload),
            payloadBlob);

    private ApplicationEnvelope ToEnvelope(IMessage message)
    {
        JsonElement? payloadElement = null;
        if (!message.Payload.IsEmpty)
        {
            payloadElement = JsonSerializer.Deserialize(message.Payload.Utf8.Span, context.GetTypeInfo<JsonElement>());
        }

        return message switch
        {
            IRequestMessage request => new ApplicationEnvelope
            {
                ProtocolVersion = request.ProtocolVersion,
                Kind = "request",
                Method = request.Method,
                Route = request.Route,
                PayloadType = request.PayloadType,
                Payload = payloadElement,
            },
            IResponseMessage response => new ApplicationEnvelope
            {
                ProtocolVersion = response.ProtocolVersion,
                Kind = "response",
                Status = response.StatusCode,
                PayloadType = response.PayloadType,
                Payload = payloadElement,
            },
            _ => throw new ArgumentException($"Unsupported application message runtime type '{message.GetType()}'.", nameof(message)),
        };
    }

    [SuppressMessage("Reliability", CA2000_WARN_OBJECT_NOT_DISPOSED, Justification = CA2000_OWNERSHIP_TRANSFER)]
    private IMessage FromEnvelope(ApplicationEnvelope envelope)
    {
        if (envelope.ProtocolVersion != ApplicationProtocolVersion.Current)
        {
            throw new ApplicationProtocolException(
                ApplicationProtocolError.VersionMismatch,
                $"Unsupported application protocol version {envelope.ProtocolVersion}; this peer speaks version {ApplicationProtocolVersion.Current}.");
        }

        if (!ApplicationPayloadTypes.IsKnown(envelope.PayloadType))
        {
            throw new ApplicationProtocolException(
                ApplicationProtocolError.UnknownPayloadType,
                $"Unknown application payload type '{envelope.PayloadType}'.");
        }

        bool hasPayload = envelope.Payload is { } element
            && element.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null;

        ValidatePayloadPresence(envelope.PayloadType, hasPayload);

        if (envelope.Kind == "request")
        {
            return FromRequestEnvelope(envelope, hasPayload);
        }

        if (envelope.Kind == "response")
        {
            return FromResponseEnvelope(envelope, hasPayload);
        }

        throw new ApplicationProtocolException(
            ApplicationProtocolError.InvalidKind,
            $"Application document kind '{envelope.Kind}' is not 'request' or 'response'.");
    }

    [SuppressMessage("Reliability", CA2000_WARN_OBJECT_NOT_DISPOSED, Justification = CA2000_OWNERSHIP_TRANSFER)]
    private IRequestMessage FromRequestEnvelope(ApplicationEnvelope envelope, bool hasPayload)
    {
        if (envelope.Status is not null)
        {
            throw new ApplicationProtocolException(
                ApplicationProtocolError.UnexpectedField,
                "A request document must not include 'status'.");
        }

        if (string.IsNullOrWhiteSpace(envelope.Method) || string.IsNullOrWhiteSpace(envelope.Route))
        {
            throw new ApplicationProtocolException(
                ApplicationProtocolError.MissingRequiredField,
                "A request document requires non-empty 'method' and 'route'.");
        }

        if (!ApplicationPayloadTypes.IsRequestPayloadType(envelope.PayloadType))
        {
            throw new ApplicationProtocolException(
                ApplicationProtocolError.PayloadTypeMismatch,
                $"Request documents cannot use response payload type '{envelope.PayloadType}'.");
        }

        return new RequestMessage(
            envelope.ProtocolVersion,
            envelope.Method,
            envelope.Route,
            envelope.PayloadType,
            CreatePayloadBlob(envelope, hasPayload));
    }

    [SuppressMessage("Reliability", CA2000_WARN_OBJECT_NOT_DISPOSED, Justification = CA2000_OWNERSHIP_TRANSFER)]
    private IResponseMessage FromResponseEnvelope(ApplicationEnvelope envelope, bool hasPayload)
    {
        if (envelope.Method is not null || envelope.Route is not null)
        {
            throw new ApplicationProtocolException(
                ApplicationProtocolError.UnexpectedField,
                "A response document must not include 'method' or 'route'.");
        }

        if (envelope.Status is not int status || status < 100 || status > 599)
        {
            throw new ApplicationProtocolException(
                ApplicationProtocolError.InvalidStatus,
                "A response document requires 'status' in the range 100..599.");
        }

        if (!ApplicationPayloadTypes.IsResponsePayloadType(status, envelope.PayloadType))
        {
            throw new ApplicationProtocolException(
                ApplicationProtocolError.PayloadTypeMismatch,
                $"Response status {status} cannot use payload type '{envelope.PayloadType}'.");
        }

        if (envelope.PayloadType == ApplicationPayloadTypes.ValidationError && status != 400)
        {
            throw new ApplicationProtocolException(
                ApplicationProtocolError.PayloadTypeMismatch,
                $"Response payload type '{ApplicationPayloadTypes.ValidationError}' requires status 400.");
        }

        ValidateWellKnownResponseRepresentation(envelope);

        return new ResponseMessage(
            envelope.ProtocolVersion,
            status,
            envelope.PayloadType,
            CreatePayloadBlob(envelope, hasPayload));
    }

    private static void ValidateWellKnownResponseRepresentation(ApplicationEnvelope envelope)
    {
        if (envelope.PayloadType is not (ApplicationPayloadTypes.Error or ApplicationPayloadTypes.ValidationError))
        {
            return;
        }

        if (envelope.Payload is not { ValueKind: JsonValueKind.Object } payload)
        {
            throw new ApplicationProtocolException(
                ApplicationProtocolError.PayloadTypeMismatch,
                $"Response payload type '{envelope.PayloadType}' requires an object payload.");
        }

        if (envelope.PayloadType == ApplicationPayloadTypes.ValidationError
            && (!payload.TryGetProperty("errors", out JsonElement errors) || errors.ValueKind != JsonValueKind.Array))
        {
            throw new ApplicationProtocolException(
                ApplicationProtocolError.PayloadTypeMismatch,
                $"Response payload type '{ApplicationPayloadTypes.ValidationError}' requires an 'errors' array.");
        }
    }

    private static void ValidatePayloadPresence(string payloadType, bool hasPayload)
    {
        if (payloadType == ApplicationPayloadTypes.Empty)
        {
            if (hasPayload)
            {
                throw new ApplicationProtocolException(
                    ApplicationProtocolError.PayloadTypeMismatch,
                    "payloadType 'empty' must not include a payload.");
            }

            return;
        }

        if (!hasPayload)
        {
            throw new ApplicationProtocolException(
                ApplicationProtocolError.PayloadTypeMismatch,
                $"payloadType '{payloadType}' requires a payload.");
        }
    }

    [SuppressMessage("Reliability", CA2000_WARN_OBJECT_NOT_DISPOSED, Justification = CA2000_OWNERSHIP_TRANSFER)]
    private IMessageBlob CreatePayloadBlob(ApplicationEnvelope envelope, bool hasPayload)
    {
        if (!hasPayload)
        {
            return BufferedJsonMessageBlob.FromUtf8(ReadOnlyMemory<byte>.Empty, context);
        }

        return BufferedJsonMessageBlob.FromUtf8(
            JsonSerializer.SerializeToUtf8Bytes(envelope.Payload!.Value, context.GetTypeInfo<JsonElement>()),
            context);
    }

    private static string ResolveRequestPayloadType(object? payload) =>
        payload is null or IEmptyPayload ? ApplicationPayloadTypes.Empty : ApplicationPayloadTypes.Data;

    private static string ResolveResponsePayloadType(IResponsePayload payload) => payload switch
    {
        IEmptyPayload => ApplicationPayloadTypes.Empty,
        ModelValidationErrorResponse => ApplicationPayloadTypes.ValidationError,
        ErrorResponse => ApplicationPayloadTypes.Error,
        _ => ApplicationPayloadTypes.Data,
    };
}
