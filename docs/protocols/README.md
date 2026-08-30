# IPC protocols

The web application and the privileged daemon communicate over a single local
connection that carries **one request and one response**, then closes.

That connection is stacked as two independently versioned protocols:

| Layer | Document | Responsibility |
| --- | --- | --- |
| Stream | (OS pipe / Unix socket / in-process duplex) | Bytes, including fragmentation |
| **ITP** | [itp.md](itp.md) | Version bootstrap, framing, packet type, application payload format, structured transport errors |
| **Application protocol** | [application-protocol.md](application-protocol.md) | Request vs response, payload type, JSON bodies |

ITP does not interpret application JSON. The application protocol does not
frame bytes, check transport versions, or recover from truncated frames.
Client and daemon orchestration compose the layers explicitly: ITP reads or
writes framed opaque bytes, while the application codec only decodes or encodes
the complete application document carried by those bytes.

Backwards compatibility with the previous newline-delimited JSON proof of
concept is **not** provided. Both peers must speak ITP v1 and application
protocol v1.

## Design decisions

- **Two versions, no negotiation.** ITP and the application protocol each
  carry their own version. A mismatch is a hard failure. There is no
  handshake that could paper over an incompatible peer.
- **Two packet types only.** `ApplicationData` and `TransportError`. Keepalive,
  multiplexing, and session types are out of scope for a single
  request/response connection.
- **Bootstrap before parsing.** Only the `ITP` magic and wire-version byte are
  stable across versions. A receiver selects a version-specific parser before
  interpreting any later bytes.
- **Classify the upper layer explicitly.** `ApplicationData` carries an ITP
  payload-format identifier. Unknown formats fail before application decoding.
- **Lengths are untrusted.** Declared payload length is compared to a maximum
  before payload allocation or body reads.
- **`payloadType` is the discriminator.** Generic `400` and validation `400`
  share a status and are distinguished by `error` vs `validation-error`, not
  by probing DTOs.
- **Payload absence is explicit.** `payloadType=empty` omits `payload`; a
  `data` document always carries the property, and JSON `null` remains a
  present value. Missing payloads and binding failures never become
  `default(T)` request objects.
- **Invalid JSON is never a valid request or response.** Required fields, kind,
  and payload-type consistency are enforced after deserialize.
- **Timeouts live on the stream, not in the frame.** `TimedStream` plus
  `CancellationToken` prevent waiting on a dead peer. The protocols themselves
  have no timeout field.
