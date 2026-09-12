# Application IPC Protocol v1

The application IPC protocol defines the JSON request and response documents exchanged between `Ufw.Web` and `Ufw.Systemd`. One document is carried as the payload of one ITP `ApplicationData` frame.

ITP owns framing and delivers complete bytes. This protocol owns application-envelope semantics. Daemon routing interprets method and route only after the envelope is valid.

The requirement words in this document describe interoperability requirements for UFW WebUI implementations.

## Versioning

Every document MUST contain `protocolVersion: 1`.

The application version is independent of the ITP wire version and independent of route versions such as `/api/v1`. A v1 application document can travel only after the ITP layer has already accepted its own wire version.

There is no application-version negotiation. A receiver MUST reject an unsupported `protocolVersion`.

## Message envelope

Every message is one UTF-8 JSON object using camelCase property names. The `kind` field explicitly distinguishes requests from responses; direction MUST NOT be inferred from whichever optional fields happen to be present.

### Request

A request without a body uses the `empty` representation:

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
  "route": "/api/v1/rules",
  "payloadType": "data",
  "payload": { "operation": "rules.add" }
}
```

### Response

Responses carry an HTTP-like integer status and do not carry request routing fields:

```json
{
  "protocolVersion": 1,
  "kind": "response",
  "status": 200,
  "payloadType": "data",
  "payload": { "active": true, "rules": [] }
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

| Field | Required for | Requirement |
| --- | --- | --- |
| `protocolVersion` | all messages | MUST equal `1` |
| `kind` | all messages | MUST be `request` or `response` |
| `method` | request | MUST be a non-empty method token |
| `route` | request | MUST be a non-empty daemon route |
| `status` | response | MUST be an integer from `100` through `599` |
| `payloadType` | all messages | MUST identify a representation defined below |
| `payload` | representation-dependent | MUST be present or absent exactly as defined by `payloadType` |

A request MUST NOT carry `status`. A response MUST NOT carry `method` or `route`.

Unknown JSON properties are ignored by application-v1 deserialization. Implementations MUST NOT depend on an unknown property affecting routing, binding, or response dispatch.

## Payload representations

`payloadType` is the representation discriminator. Receivers MUST NOT infer a payload type by attempting multiple DTO deserializations.

| `payloadType` | Payload rule | Allowed use |
| --- | --- | --- |
| `empty` | `payload` MUST be absent | bodyless request or successful response without a representation |
| `data` | `payload` MUST be present; any JSON value, including `null` | request body or successful response body |
| `error` | object payload, optional `message` | response status `400..599` |
| `validation-error` | object payload with `errors` array and optional `message` | response status exactly `400` |

Requests MAY use only `empty` and `data`. Successful responses MUST use `empty` or `data`. Failure responses MUST use `error` or `validation-error`.

Payload presence is distinct from the JSON value. Under `data`, `payload: null` is a present payload. Under `empty`, the `payload` property MUST be omitted entirely, including `payload: null`.

## Decode procedure

Before routing, the receiver:

1. parses exactly one complete JSON document and requires an object root;
2. validates `protocolVersion`, `kind`, and `payloadType`;
3. validates request or response metadata for the declared direction;
4. validates representation legality and payload presence;
5. validates the structural shape of well-known error representations;
6. creates a direction-specific runtime message backed by the buffered payload bytes.

The following are protocol errors:

- zero-length application data;
- invalid JSON, a non-object root, or an empty object missing required fields;
- missing or unsupported protocol version, kind, or representation;
- missing request method/route or response status;
- request metadata on a response or response metadata on a request;
- response-only representations on requests;
- success/error status classes paired with an incompatible representation;
- `validation-error` without status `400` or without an `errors` array;
- `empty` with a `payload` property;
- any non-empty representation without a `payload` property.

When the daemon has reached the application layer, a malformed application document is returned as a `400` `error` response. A client receiving a malformed response treats it as an application-protocol failure.

## Routing and typed binding

Routing operates only on a valid request envelope. It consumes the request method and route directly and selects one typed endpoint contract.

Envelope validity and endpoint payload validity are separate checks:

- a body-taking endpoint MUST receive `payloadType: data`;
- a bodyless endpoint MUST receive `payloadType: empty`;
- a present JSON `null` is not equivalent to an absent body and does not satisfy an endpoint requiring a materialized non-null request object;
- valid JSON values such as `{}`, `0`, and `false` remain present values and are accepted when normal binding to the routed request type accepts them;
- binding failure returns `400` and MUST NOT invoke endpoint/domain logic.

An unknown route returns `404`. A recognized route with an unsupported method returns `501`.

## Daemon route set

The current application-v1 daemon routes are:

| Method | Route | Purpose | Signed intent required |
| --- | --- | --- | --- |
| `GET` | `/api/v1/intent/context` | read deployment identity and signed-intent protocol version | no |
| `GET` | `/api/v1/network-interfaces` | enumerate current host interface names | no |
| `GET` | `/api/v1/rules` | read authoritative UFW state | no |
| `POST` | `/api/v1/rules` | append a rule | yes, `rules.add` |
| `POST` | `/api/v1/rules/insert` | insert a concrete-family rule before or after an occurrence in the exact reviewed snapshot | yes, `rules.insert` |
| `PUT` | `/api/v1/rules/order` | reorder the exact reviewed rule snapshot | yes, `rules.reorder` |
| `DELETE` | `/api/v1/rules` | delete a concrete rule | yes, `rules.delete` |

The interface route carries host-observed names only. ASP-owned UUIDs, comments, and visibility metadata are intentionally outside IPC.

## Response semantics

Application-level failures use the same envelope as successful results.

A generic application error is represented as:

```json
{
  "protocolVersion": 1,
  "kind": "response",
  "status": 400,
  "payloadType": "error",
  "payload": { "message": "Malformed request." }
}
```

A model-validation failure uses the distinct `validation-error` representation:

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

The daemon maps successful empty results to `empty`, successful DTO results to `data`, model-validation failures to `400 validation-error`, and other application errors to `error` with the DTO-defined status. Verified `rules.insert` and `rules.reorder` transactions are returned over IPC as typed `data` results even when their state-conditioned goal was not reached. Insertion preserves completed, stale-baseline, precondition-failed, and state-uncertain outcomes; reorder additionally preserves partial-completion and recovery outcomes. This keeps authoritative final snapshots and operation reports intact across the daemon boundary. Signature, replay, malformed-intent, and other authorization failures remain ordinary application errors.

## Failures and cancellation

The application protocol has no timeout or cancellation field. The connection owner applies the idle and overall deadlines described in the [protocol overview](README.md).

| Condition | Owning layer | Result |
| --- | --- | --- |
| invalid/truncated ITP frame | ITP | connection-scoped transport failure; application decoder is not invoked |
| invalid application-v1 document | application protocol | daemon returns `400 error`; client reports protocol failure |
| unknown route | routing | `404 error` |
| unsupported method | routing | `501 error` |
| payload cannot bind to endpoint request type | binding | `400 error`; endpoint is not invoked |
| model validation failure | application endpoint | `400 validation-error` |
| unhandled endpoint exception | application endpoint/framework | `500 error` when safely mappable |

External caller cancellation and daemon shutdown remain cancellation signals rather than protocol errors.
