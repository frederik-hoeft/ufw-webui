# Application IPC protocol v1

The application IPC protocol defines the request and response documents exchanged
between `Ufw.Web` and `Ufw.Systemd`. Its unit is one UTF-8 JSON document carried
inside an ITP `ApplicationData` frame.

The protocol owns application-message semantics only. ITP establishes framing
compatibility and delivers complete bytes; daemon routing interprets the method
and route only after the application envelope is valid.

## Versioning and boundaries

The application protocol carries its own `protocolVersion`, currently `1`. This
version is independent of both the ITP wire version and route versions such as
`/api/v1`:

- the ITP version defines how bytes are framed;
- the application protocol version defines the JSON envelope and representation
  rules;
- the route version defines an endpoint/controller contract.

A different application protocol version can in principle travel inside the same
ITP version, provided both peers implement that application version. There is no
version negotiation: a document whose `protocolVersion` is not supported is an
application-protocol failure.

## Envelope model

Every application message is a single JSON object using camelCase property names.
The envelope carries direction explicitly through `kind`; request and response
metadata are not inferred from which optional fields happen to be present.

### Request

A no-body request contains method and route metadata and uses the `empty`
representation:

```json
{
  "protocolVersion": 1,
  "kind": "request",
  "method": "GET",
  "route": "/api/v1/rules",
  "payloadType": "empty"
}
```

A request with a body uses `data` and includes `payload`:

```json
{
  "protocolVersion": 1,
  "kind": "request",
  "method": "POST",
  "route": "/api/v1/echo",
  "payloadType": "data",
  "payload": { "message": "hello" }
}
```

### Response

Responses carry an HTTP-like integer status rather than method/route metadata:

```json
{
  "protocolVersion": 1,
  "kind": "response",
  "status": 200,
  "payloadType": "data",
  "payload": { "message": "hello" }
}
```

A response with no representation uses `empty` and omits `payload`:

```json
{
  "protocolVersion": 1,
  "kind": "response",
  "status": 200,
  "payloadType": "empty"
}
```

### Envelope fields

| Field | Required | Meaning |
| --- | --- | --- |
| `protocolVersion` | always | Application protocol version; v1 requires `1` |
| `kind` | always | `request` or `response` |
| `method` | request only | Non-empty method token; unsupported methods are rejected by routing with `501` |
| `route` | request only | Non-empty daemon route, including its API route version |
| `status` | response only | Integer status in `100..599` |
| `payloadType` | always | Representation identifier described below |
| `payload` | representation-dependent | JSON value; omitted only for `empty` |

A request must not carry `status`. A response must not carry `method` or `route`.
Unknown JSON properties are ignored by the source-generated `System.Text.Json`
configuration, but all required protocol fields and cross-field invariants are
validated explicitly.

## Representations and payload presence

`payloadType` is the representation discriminator. Receivers never determine the
body type by trying several DTOs until one happens to deserialize.

| `payloadType` | Payload contract | Direction / status |
| --- | --- | --- |
| `empty` | `payload` must be absent | Request without a body; successful response without a representation |
| `data` | `payload` must be present; any JSON value, including `null` | Request body; successful response body |
| `error` | Object payload with optional `message` | Failure response (`400..599`) |
| `validation-error` | Object payload with `errors` array and optional `message` | Validation failure response with status `400` |

Requests may use only `empty` and `data`. Successful responses use `empty` or
`data`; failure responses use `error` or `validation-error`.
`validation-error` is valid only with status `400`.

Payload presence and JSON value are separate states. Under `data`, an explicit
`payload: null` is a present payload. Under `empty`, the `payload` property must
not exist, including as `payload: null`. This distinction prevents an absent body
from being materialized as `default(T)`.

## Decode and validation lifecycle

Application decoding establishes a valid runtime envelope before routing:

1. Parse one complete JSON document as an object.
2. Require application protocol version `1` and a recognized `kind` and
   `payloadType`.
3. Validate direction-specific metadata: requests require non-empty method and
   route; responses require a status in `100..599`; fields from the opposite
   direction are rejected.
4. Validate representation legality and payload presence.
5. Validate the structural shape of well-known error representations.
6. Create either an `IRequestMessage` or `IResponseMessage` whose payload is
   backed by buffered application bytes.

The following classes of input are protocol errors rather than partially valid
messages:

- zero-length application data, non-object JSON, an empty object, or invalid JSON;
- missing or unsupported application protocol version, message kind, or
  representation identifier;
- missing request method/route or response status;
- request/response metadata from the wrong direction;
- response-only representations on requests;
- success/error status classes paired with the wrong representation;
- `validation-error` with a status other than `400` or without an `errors` array;
- `empty` with a `payload` property, or a non-empty representation without one.

Malformed application documents received by the daemon become a `400` `error`
response when the request reached the application layer. The client treats a
malformed response as `ApplicationProtocolException`.

## Runtime messages and routing

The decoded runtime model keeps direction structurally explicit:

- `IRequestMessage` has non-null `Method` and `Route`;
- `IResponseMessage` has an integer `StatusCode`;
- both expose protocol version, representation identifier, and buffered payload
  through `IMessage`.

Routing therefore never receives a generic envelope with nullable alternate
request/response fields. It consumes `IRequestMessage.Method` and
`IRequestMessage.Route` directly. Client response dispatch consumes
`IResponseMessage.StatusCode` and `PayloadType` directly.

Controller response DTOs retain their `IIdentifiable` contract for generated
endpoint mapping. That DTO identity is separate from the application envelope
and is not an on-wire message identifier.

## Typed request binding

Envelope validity and route-specific DTO validity are separate stages. The
application codec preserves the payload bytes and presence state; the selected
endpoint decides which CLR request type those bytes must satisfy.

- A body-taking endpoint requires `payloadType=data`. Absence is a `400` before
  deserialization and the endpoint is not invoked.
- A no-body endpoint requires `payloadType=empty`. A present body is rejected
  rather than silently discarded.
- JSON `null` is a present value, but body-taking daemon endpoints require a
  materialized non-null request object, so `null` produces `400` without endpoint
  invocation.
- Valid default-like JSON values such as `{}`, `0`, and `false` remain present
  values. They are accepted when normal JSON binding to the routed request type
  accepts them.
- Invalid JSON shape or a deserialization failure produces `400`; it never falls
  back to `default(T)` and never invokes controller/domain code.

## Response semantics

The daemon uses the same application envelope for normal results and
application-level failures. `payloadType` distinguishes response representations
that share a status code.

```json
{
  "protocolVersion": 1,
  "kind": "response",
  "status": 400,
  "payloadType": "error",
  "payload": { "message": "Malformed request." }
}
```

```json
{
  "protocolVersion": 1,
  "kind": "response",
  "status": 400,
  "payloadType": "validation-error",
  "payload": {
    "message": "One or more validation errors occurred.",
    "errors": [
      { "propertyName": "port", "errorMessage": "Port is out of range." }
    ]
  }
}
```

The encoder maps production response DTOs as follows:

- `OkResponse` / `IEmptyPayload` -> `empty` with no `payload`;
- other `OkResponseBase` values -> `data`;
- `ModelValidationErrorResponse` -> status `400`, `validation-error`;
- other `ErrorResponse` values -> `error` using the DTO status.

A no-body request uses the dedicated `empty` request path. Any typed request
value, including CLR `null`, is encoded as `data`; CLR `null` becomes JSON
`null`.

## Serialization and ownership

Production application serialization uses the source-generated
`MessageJsonSerializerContext`. The daemon and production client do not rely on a
reflection fallback for protocol-envelope or production request/response DTO
metadata. Test infrastructure may extend metadata resolution for test-only DTOs,
but that resolver is not part of production DI.

Application payloads are fully buffered. Decoded messages do not retain the ITP
stream, so a caller may read a response payload after the one-exchange transport
connection has been released.

## Timeouts, cancellation, and failures

The application protocol contains no timeout field. Connection owners impose the
per-I/O idle timeout and overall request deadline described in the
[protocol overview](README.md). The overall deadline includes application
processing. External caller or daemon-shutdown cancellation remains cancellation
rather than being translated into an internal timeout.

| Condition | Owning layer | Result |
| --- | --- | --- |
| Invalid/truncated ITP frame or unsupported ITP metadata | ITP | Connection-scoped ITP failure; application codec is not invoked |
| Invalid application v1 document | Application protocol | Daemon returns `400` `error`; client raises `ApplicationProtocolException` |
| Unknown route | Routing | `404` `error` |
| Unsupported method | Routing | `501` `error` |
| Payload cannot bind to routed request type | Application binding | `400` `error`; endpoint not invoked |
| Model validation failure | Application | `400` `validation-error` |
| Controller exception | Application | `500` `error` |
