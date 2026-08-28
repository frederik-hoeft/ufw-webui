using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Protocol;
using Ufw.Ipc.Shared.Transport.Itp;
using Ufw.Roslyn.Controllers;
using Ufw.Roslyn.Json;

namespace Ufw.Ipc.Shared.Serialization.Json;

/// <summary>
/// Application-level codec. Stream <see cref="ReadAsync"/> / <see cref="WriteAsync"/>
/// compose ITP framing so callers can still treat a connection as message-oriented,
/// but encode/decode themselves never inspect ITP bytes.
/// </summary>
public sealed class JsonMessageSerializer(AotJsonSerializerContext context, ItpOptions? itpOptions = null) : IMessageSerializer
{
    private readonly ItpOptions _itpOptions = itpOptions ?? ItpOptions.Default;

    [SuppressMessage("Reliability", CA2000_WARN_OBJECT_NOT_DISPOSED, Justification = CA2000_OWNERSHIP_TRANSFER)]
    public ValueTask<IMessage> SerializeAsync<T>(string id, string? method, T payload, CancellationToken cancellationToken)
    {
        BufferedJsonMessageBlob payloadBlob = BufferedJsonMessageBlob.CreateFrom(payload, context);
        IMessage message = CreateMessage(id, method, payload, payloadBlob);
        return ValueTask.FromResult(message);
    }

    [SuppressMessage("Reliability", CA2000_WARN_OBJECT_NOT_DISPOSED, Justification = CA2000_OWNERSHIP_TRANSFER)]
    public ValueTask<IMessage> SerializeAsync(string id, string? method, object? payload, Type type, CancellationToken cancellationToken)
    {
        BufferedJsonMessageBlob from = BufferedJsonMessageBlob.CreateFrom(payload, type, context);
        IMessage message = CreateMessage(id, method, payload, from);
        return ValueTask.FromResult(message);
    }

    public ValueTask<IMessage> SerializeAsync<T>(T payload, CancellationToken cancellationToken) where T : IIdentifiable
    {
        if (payload is IResponseMessage responseMessage)
        {
            return SerializeAsync(StatusId(responseMessage.StatusCode), method: null, payload, cancellationToken);
        }

        return SerializeAsync(payload.Id, payload.Method, payload, cancellationToken);
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

    public async Task<IMessage> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ItpConnection itp = new(stream, _itpOptions);
        ItpFrame frame = await itp.ReadAsync(cancellationToken).ConfigureAwait(false);
        return Decode(frame.Payload);
    }

    public async Task WriteAsync(Stream stream, IMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(message);
        byte[] payload = Encode(message);
        ItpConnection itp = new(stream, _itpOptions);
        await itp.WriteApplicationDataAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    private static IMessage CreateMessage(string id, string? method, object? payload, IMessageBlob payloadBlob)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        string payloadType = ResolvePayloadType(payload);
        if (method is null
            && int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out int status)
            && status is >= 100 and <= 599)
        {
            return new Message(
                ApplicationMessageKind.Response,
                ApplicationProtocolVersion.Current,
                method: null,
                route: null,
                status,
                payloadType,
                payloadBlob);
        }

        return new Message(
            ApplicationMessageKind.Request,
            ApplicationProtocolVersion.Current,
            method,
            id,
            statusCode: null,
            payloadType,
            payloadBlob);
    }

    private ApplicationEnvelope ToEnvelope(IMessage message)
    {
        JsonElement? payloadElement = null;
        if (!message.Payload.IsEmpty)
        {
            payloadElement = JsonSerializer.Deserialize(message.Payload.Utf8.Span, context.GetTypeInfo<JsonElement>());
        }

        if (message.Kind == ApplicationMessageKind.Request)
        {
            return new ApplicationEnvelope
            {
                ProtocolVersion = message.ProtocolVersion,
                Kind = "request",
                Method = message.Method,
                Route = message.Route ?? message.Id,
                PayloadType = message.PayloadType,
                Payload = payloadElement,
            };
        }

        return new ApplicationEnvelope
        {
            ProtocolVersion = message.ProtocolVersion,
            Kind = "response",
            Status = message.StatusCode,
            PayloadType = message.PayloadType,
            Payload = payloadElement,
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

        if (envelope.PayloadType == ApplicationPayloadTypes.Empty)
        {
            if (hasPayload)
            {
                throw new ApplicationProtocolException(
                    ApplicationProtocolError.PayloadTypeMismatch,
                    "payloadType 'empty' must not include a payload.");
            }
        }
        else if (!hasPayload)
        {
            throw new ApplicationProtocolException(
                ApplicationProtocolError.PayloadTypeMismatch,
                $"payloadType '{envelope.PayloadType}' requires a payload.");
        }

        if (envelope.Kind == "request")
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

            return new Message(
                ApplicationMessageKind.Request,
                envelope.ProtocolVersion,
                envelope.Method,
                envelope.Route,
                statusCode: null,
                envelope.PayloadType,
                CreatePayloadBlob(envelope, hasPayload));
        }

        if (envelope.Kind == "response")
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

            return new Message(
                ApplicationMessageKind.Response,
                envelope.ProtocolVersion,
                method: null,
                route: null,
                status,
                envelope.PayloadType,
                CreatePayloadBlob(envelope, hasPayload));
        }

        throw new ApplicationProtocolException(
            ApplicationProtocolError.InvalidKind,
            $"Application document kind '{envelope.Kind}' is not 'request' or 'response'.");
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

    private static string ResolvePayloadType(object? payload) => payload switch
    {
        null or IEmptyPayload => ApplicationPayloadTypes.Empty,
        ModelValidationErrorResponse => ApplicationPayloadTypes.ValidationError,
        ErrorResponse => ApplicationPayloadTypes.Error,
        _ => ApplicationPayloadTypes.Data,
    };

    private static string StatusId(HttpStatusCode statusCode) =>
        ((int)statusCode).ToString(CultureInfo.InvariantCulture);
}
