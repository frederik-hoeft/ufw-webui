# IPC protocol architecture

`Ufw.Web` communicates with the privileged `Ufw.Systemd` daemon through a local,
connection-oriented IPC channel. Each connection carries exactly one application
request and, when the request can be processed far enough to produce one, one
response. The connection is then closed.

The IPC stack deliberately separates stream transport, wire framing, application
message semantics, and daemon routing. Each layer validates only the contract it
owns and passes a fully validated unit to the layer above it.

## Protocol stack

| Layer | Unit | Responsibility |
| --- | --- | --- |
| Local stream / stream security | bytes | Connection establishment, ordered byte delivery, optional stream-security wrapping, I/O cancellation |
| [ITP](itp.md) | frame | Wire-version bootstrap, framing, packet kind, application payload format, size limits, structured transport errors |
| [Application protocol](application-protocol.md) | UTF-8 JSON document | Request/response direction, method/route or status, representation identifier, payload presence |
| Daemon routing and binding | typed request | Route selection, request DTO binding, controller invocation, response DTO production |

ITP treats application data as opaque bytes after classifying its payload format.
The JSON application codec works only with complete application-document bytes and
has no stream or ITP dependency. Routing sees only a valid application request;
malformed framing and malformed application envelopes never reach controller code.

## Independent version domains

Four different version identifiers exist because they answer different
compatibility questions:

| Version domain | Example | Governs |
| --- | --- | --- |
| ITP wire version | ITP `1` | How bytes after the stable ITP preamble are framed and interpreted |
| Application IPC protocol version | `protocolVersion: 1` | Request/response envelope and representation semantics |
| API route version | `/api/v1/rules` | Daemon endpoint/controller contract |
| Signed-intent version | intent `version: 2` | Canonical mutation authorization and rule-signing semantics |

These versions are intentionally independent. ITP does not negotiate application
versions, and an API route version is not a substitute for a wire or signing
protocol version. A peer must understand the ITP wire version before it can obtain
an application document, and it must understand the application protocol version
before routing the request. Signed-intent versioning applies only to mutation
authorization carried inside otherwise valid application requests.

There is no protocol negotiation or fallback. An unsupported version fails at
the layer that owns it.

## Exchange lifecycle

A normal request follows one ownership path:

1. The client application codec creates a request document from the method,
   route, and optional typed payload.
2. ITP writes that document as one `ApplicationData` frame over the secured
   local stream.
3. The daemon validates and fully buffers the ITP frame before passing its
   application bytes upward.
4. The application codec validates the JSON envelope and produces an
   `IRequestMessage` with explicit payload presence.
5. Routing selects an endpoint. Body-taking endpoints bind the buffered payload
   to the routed request type before controller code is invoked.
6. The endpoint response is encoded as an application response document and
   written as one ITP `ApplicationData` frame.
7. The client fully reads and decodes the response before releasing the
   transport connection. Response payloads remain readable from their buffered
   application bytes after the stream is closed.

A connection carries no reusable ITP session state and no request correlation
identifier because only one exchange is allowed per connection.

## Failure boundaries

Failures remain scoped to the layer that can classify them:

- ITP rejects invalid magic, unsupported wire versions, truncated frames,
  unknown packet kinds or application payload formats, and unsafe lengths before
  application decoding.
- A recognized v1 framing failure may be returned as `TransportError` only when
  enough framing context exists to know that a v1 reply is safe. Incoming
  `TransportError` frames are terminal notifications and are never answered with
  another transport error.
- The application codec rejects malformed JSON, incompatible application
  versions, illegal request/response field combinations, and invalid
  representation semantics.
- Route-specific binding failures are application `400` responses and do not
  invoke the endpoint.
- Expected peer, transport, timeout, and protocol failures terminate only the
  current daemon connection. Unexpected daemon/framework failures remain
  observable by faulting the worker/application rather than being absorbed as
  connection errors.

## Time bounds and cancellation

Timeouts are connection policy rather than wire fields. Both peers distinguish a
per-I/O idle timeout from an overall request deadline. The idle timeout releases a
connection whose current read or write stops making progress; the request deadline
bounds the complete exchange even if a peer continuously trickles data within the
idle window.

External client cancellation and daemon shutdown remain cancellation. Internal
deadline expiry is reported as a timeout. Either configured timeout can be
explicitly disabled with `Timeout.InfiniteTimeSpan`.

## Detailed protocol references

- [ITP v1](itp.md) defines the stable bootstrap, v1 frame layout, packet kinds,
  payload-format registry, transport-error format, and framing failure rules.
- [Application IPC protocol v1](application-protocol.md) defines the JSON
  envelope, representation identifiers, payload-presence contract, typed binding,
  response semantics, and application-level failures.
- [Signed mutation intent v2](signed-intent.md) defines deployment-scoped user
  authorization for AddRule and DeleteRule, canonicalization, replay protection,
  and semantic rule identity.
