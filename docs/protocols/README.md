# IPC protocols

The web application and the privileged daemon communicate over a single local
connection that carries **one request and one response**, then closes.

That connection is stacked as two independently versioned protocols:

| Layer | Document | Responsibility |
| --- | --- | --- |
| Stream | (OS pipe / Unix socket / in-process duplex) | Bytes, including fragmentation |
| **ITP** | [itp.md](itp.md) | Framing, transport version, packet type, structured transport errors |
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
- **Length and CRC are untrusted until checked.** Declared payload length is
  compared to a maximum before any allocation. CRC-32 is verified before the
  payload is handed to the application decoder.
- **Header layout is frozen.** Bytes 0–9 stay put so a v1 peer can skip an
  unknown-version frame and reply `VersionMismatch`.
- **`payloadType` is the discriminator.** Generic `400` and validation `400`
  share a status and are distinguished by `error` vs `validation-error`, not
  by probing DTOs.
- **Empty or invalid JSON is never a valid request or response.** Required
  fields, kind, and payload-type consistency are enforced after deserialize.
  `JsonException` is not converted into `default(T)`.
- **Timeouts live on the stream, not in the frame.** `TimedStream` plus
  `CancellationToken` prevent waiting on a dead peer. The protocols themselves
  have no timeout field.
