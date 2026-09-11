# IPC Protocols

`Ufw.Web` and `Ufw.Systemd` communicate over a local, connection-oriented stream. The protocol stack deliberately separates byte framing, application envelopes, route contracts, and privileged mutation authorization so each layer can reject incompatibility without guessing about the layer above it.

These documents describe project protocols, not external standards. The words **MUST**, **MUST NOT**, **SHOULD**, and **MAY** are used pragmatically to distinguish required interoperability behavior from implementation choices.

## Protocol stack

| Layer | Unit | Contract |
| --- | --- | --- |
| Local stream and optional TLS | bytes | ordered transport, peer connection, I/O cancellation |
| [IPC Transport Protocol (ITP) v1](itp.md) | frame | version bootstrap, bounded framing, packet kind, payload format, transport errors |
| [Application IPC protocol v1](application-protocol.md) | JSON document | request/response direction, route/method or status, payload representation |
| Daemon routing | typed request/response | route selection, request binding, endpoint invocation |
| [Signed mutation intent v2](signed-intent.md) | signed mutation envelope | administrator authorization for privileged firewall changes |

ITP treats application payload bytes as opaque after identifying their registered format. The application protocol operates only on complete payload bytes. Routing receives only a structurally valid request envelope. Signed-intent verification applies only to routes that mutate privileged firewall state.

## Version domains

The stack has independent versions because each version answers a different compatibility question.

| Version | Current value | Governs |
| --- | ---: | --- |
| ITP wire version | `1` | bytes after the stable ITP preamble |
| Application protocol | `1` | JSON request/response envelope and payload representations |
| Daemon route version | `/api/v1/...` | typed endpoint contract |
| Signed-intent protocol | `2` | canonical mutation authorization and rule semantics |

A peer MUST reject an unsupported version at the layer that owns it. There is no negotiation or fallback between versions.

A route version does not imply a wire version, and an application-protocol version does not imply a signed-intent version. In particular, unsigned read routes and signed mutation routes can coexist inside the same application-protocol version.

## Connection lifecycle

Each connection carries one request/response exchange:

1. the client serializes one application request;
2. ITP sends it in one `ApplicationData` frame;
3. the daemon validates the complete frame before decoding application JSON;
4. the application codec validates the envelope before routing;
5. routing binds the payload to the selected endpoint contract;
6. the daemon serializes at most one application response and sends it in one `ApplicationData` frame;
7. the connection is closed.

There is no reusable session state, multiplexing, or request correlation identifier at the IPC layer. A second request uses a new connection.

## Failure ownership

Failures stay with the layer that can classify them reliably.

ITP owns malformed framing, unsupported wire versions, unsafe lengths, packet kinds, and payload formats. The application protocol owns malformed JSON envelopes and representation invariants. Routing owns unknown routes, unsupported methods, and route-specific binding failures. Signed-intent verification owns privileged mutation authorization.

Expected peer, I/O, timeout, stream-security, and protocol failures are connection-scoped. They must not terminate a daemon worker that can safely accept a later peer. Unexpected daemon/framework failures are not reclassified as peer errors and remain observable by faulting the owning worker/application.

## Time bounds

Timeouts are connection policy rather than protocol fields. Both peers distinguish:

- a per-I/O idle timeout, which bounds an individual read or write that stops making progress;
- an overall request deadline, which bounds the complete exchange even while partial I/O continues.

Caller cancellation and daemon shutdown remain cancellation signals. They are not encoded as protocol messages or converted into internal timeout semantics.

## Protocol documents

- [ITP v1](itp.md) defines the stable bootstrap, v1 frame layout, packet registry, transport errors, and receiver requirements.
- [Application IPC protocol v1](application-protocol.md) defines the JSON envelope, payload representations, typed binding rules, and application-level errors.
- [Signed mutation intent v2](signed-intent.md) defines the browser-to-daemon authorization contract for add and delete operations, including canonicalization, replay protection, and semantic rule identity.

For the architectural role of IPC, see [UFW WebUI Architecture](../architecture/architecture-overview.md). For production socket ownership and optional TLS/mTLS, see [Deployment configuration](../deployment/configuration.md).
