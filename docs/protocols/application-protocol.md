# Application-level IPC protocol v1

This protocol sits **on top of ITP**. Its unit of exchange is the payload of
a single ITP `ApplicationData` packet: one UTF-8 JSON document.

The application protocol is versioned independently of ITP. A future
application v2 can travel in ITP v1 frames, and a future ITP v2 can carry
application v1 documents, as long as each layer validates its own version.

## Purpose

The previous POC used one implicit envelope for both directions:

- `MessageHeader.Context` was a route on the way in and an HTTP status
  string on the way out
- `400` validation failures and `400` generic failures were distinguished by
  *trying* to deserialize one DTO and falling back to the other
- `JsonException` was swallowed and became `default(T)`, so empty or
  invalid bytes could surface as a "valid" request or response

v1 makes those distinctions explicit and rejects anything that is not a
complete, well-typed document.

## Document shape

A single JSON object. Property names are camelCase. Unknown properties are
ignored (source-generated `System.Text.Json` defaults). Missing **required**
properties make the document invalid.

### Request

```json
{
  "protocolVersion": 1,
  "kind": "request",
  "method": "GET",
  "route": "/api/v1/rules",
  "payloadType": "empty"
}
```

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

```json
{
  "protocolVersion": 1,
  "kind": "response",
  "status": 200,
  "payloadType": "empty"
}
```

```json
{
  "protocolVersion": 1,
  "kind": "response",
  "status": 200,
  "payloadType": "data",
  "payload": { "message": "hello" }
}
```

```json
{
  "protocolVersion": 1,
  "kind": "response",
  "status": 400,
  "payloadType": "error",
  "payload": { "message": "Malformed request: Missing required fields." }
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

## Fields

| Field | Required | Meaning |
| --- | --- | --- |
| `protocolVersion` | yes | Application protocol version. Must be `1`. |
| `kind` | yes | `"request"` or `"response"`. No other value is valid. |
| `method` | request only | Non-empty method token (`GET`, `POST`, `PUT`, `DELETE`, or another token that routing will reject with `501`) |
| `route` | request only | Non-empty route, including the leading `/` |
| `status` | response only | Integer HTTP-like status in `100..599` |
| `payloadType` | yes | One of the well-known types below |
| `payload` | depends | JSON value; omitted when `payloadType` is `empty` |

A request document must not carry `status`. A response document must not
carry `method` or `route`. Extra direction-specific fields make the document
invalid.

`kind` is never inferred from which fields happen to be present.

## Well-known payload types

`payloadType` is an explicit discriminator. Receivers must **not** guess the
body by attempting several DTO types.

| `payloadType` | `payload` | Used for |
| --- | --- | --- |
| `empty` | must be omitted, `null`, or absent | Requests with no body; responses with no representation (`OkResponse`) |
| `data` | required JSON value (normally an object) | Typed request bodies and typed success-response bodies |
| `error` | required object `{ "message": string? }` | Generic failure responses |
| `validation-error` | required object `{ "message": string?, "errors": [ { "propertyName", "errorMessage" } ] }` | `400` caused by model validation |

Requests may use only `empty` or `data`. Responses below status `400` use
`empty` or `data`; failure responses (`400..599`) use `error` or
`validation-error`. `validation-error` is valid only with `status: 400`.
This keeps generic and validation `400` responses unambiguous without probing
multiple DTO types.

An unknown `payloadType` is not a valid request or response. The decoder
rejects the document; it does not produce a default object.

## Decoder rules

The following inputs are **not** a valid request or response object. The
decoder throws `ApplicationProtocolException` (or, on the daemon, converts
that into a `400` / `payloadType=error` response):

- zero-length ITP payload (also rejected by ITP as `EmptyApplicationPayload`)
- non-object JSON (`null`, array, string, number, `true`/`false`)
- empty object `{}`
- JSON that fails to parse
- `protocolVersion` missing or not `1`
- `kind` missing or not `"request"` / `"response"`
- `payloadType` missing or not in the well-known set
- request missing `method` or `route`, or either is empty/whitespace
- response missing `status`, or `status` outside `100..599`
- request that includes `status`, or response that includes `method`/`route`
- request using response-only `payloadType=error` or `payloadType=validation-error`
- response whose success/error status class does not match its representation (`data`/`empty` vs. `error`/`validation-error`)
- response using `payloadType=validation-error` with a status other than `400`
- `payloadType=empty` with a present non-null payload
- `payloadType` other than `empty` with a missing or `null` payload
- `payloadType=validation-error` whose `errors` array is missing

`JsonException` is never converted into `default(T)`. A typed payload
deserialize that fails is a protocol error, not an empty DTO.

On the client, a decoded document whose `kind` is not `response` is a
protocol error. On the daemon, a decoded document whose `kind` is not
`request` is answered with `400`.

## Encoder rules

- `OkResponse` / `IEmptyPayload` → `payloadType=empty`, no `payload`
- any other `OkResponseBase` → `payloadType=data`
- `ModelValidationErrorResponse` → `status=400`, `payloadType=validation-error`
- any other `ErrorResponse` → `payloadType=error` (status taken from the DTO)
- request DTO that is `IEmptyPayload` or `null` → `payloadType=empty`
- any other request DTO → `payloadType=data`

Serialization uses the source-generated `MessageJsonSerializerContext`.
Reflection-based JSON is not used on the production path.

## Mapping onto in-process types

The decoded runtime model preserves the wire direction explicitly:

- `IRequestMessage` carries a non-null method and route;
- `IResponseMessage` carries an integer status code;
- both share application protocol version, representation identifier, and payload through `IMessage`.

There is no context-dependent runtime message identifier. Routing consumes
`IRequestMessage.Method` and `IRequestMessage.Route` directly, while client
response dispatch consumes `IResponseMessage.StatusCode` and `PayloadType`.
This prevents request-only and response-only metadata from becoming nullable
alternate states on one generic message object.

Controller response DTOs retain their existing `IIdentifiable` contract for
source-generated endpoint mapping. That DTO-level identifier is separate from
the decoded application-envelope runtime model.

## Timeouts and cancellation

The application protocol has no timeout field of its own. A peer that stops
writing is unblocked by the ITP/stream timeout and by `CancellationToken`.
A request whose body cannot be materialized as the endpoint's DTO is a `400`,
not a hang.

## Failure split

| Symptom | Layer | On-wire result |
| --- | --- | --- |
| Bad magic, short frame, wrong ITP version, unknown ITP packet type or application payload format | ITP | Connection-scoped ITP failure; recognized v1 failures may return `TransportError` |
| Valid ITP frame whose JSON is not a v1 request/response | Application | Daemon: `400` + `error`. Client: `ApplicationProtocolException` |
| Valid request, unknown route | Application | `404` + `error` |
| Valid request, unknown method | Application | `501` + `error` |
| Valid request, validation failure | Application | `400` + `validation-error` |
| Controller exception | Application | `500` + `error` |
