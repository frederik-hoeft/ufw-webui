# IPC Transport Protocol (ITP) v1

ITP is the framing protocol used on the local stream between the web application and daemon. It establishes wire compatibility before application decoding, bounds allocation from untrusted lengths, reassembles frames from arbitrary stream fragments, and provides a small transport-error vocabulary when a v1 peer can be identified safely.

ITP does not define application routes, JSON semantics, authentication, sessions, multiplexing, or mutation authorization. A connection carries at most one application exchange.

The requirement words in this document describe interoperability requirements for UFW WebUI implementations; they are not a claim of external standardization.

## Stable bootstrap

Every frame begins with a four-byte preamble that is independent of the version-specific frame format.

| Offset | Size | Field | v1 value |
| ---: | ---: | --- | --- |
| `0` | 3 | magic | ASCII `ITP` (`49 54 50` hex) |
| `3` | 1 | version | `01` hex |

A receiver MUST read and validate only this preamble before selecting a version-specific parser.

If the magic is invalid, the receiver MUST close the connection and MUST NOT send a transport-error frame because the peer has not been established as an ITP peer.

If the version is unsupported, the receiver MUST close the connection without interpreting any version-specific bytes. A v1 implementation MUST NOT assume that another version understands the v1 error-frame format.

There is no version negotiation or fallback handshake.

## Version 1 frame

After the stable preamble, v1 appends a six-byte header followed by the declared payload. Multi-byte integers use network byte order (big-endian).

| Offset | Size | Field | Encoding |
| ---: | ---: | --- | --- |
| `0` | 3 | magic | ASCII `ITP` |
| `3` | 1 | version | unsigned byte, `1` |
| `4` | 1 | packet type | registry below |
| `5` | 1 | payload format | registry below |
| `6` | 4 | payload length | unsigned 32-bit big-endian |
| `10` | N | payload | exactly `payload length` bytes |

The fixed v1 header is 10 bytes. v1 has no flags, trailer, checksum, or reserved extension bytes.

`payload length` is untrusted. A receiver MUST reject a value above its configured limit before allocating or reading the payload. The project default limit is 16 MiB.

A stream read is not required to return all requested bytes. Receivers MUST continue reading until each fixed field and the declared payload are complete or the stream ends/cancels.

ITP relies on the ordered stream for reliable delivery. It does not add a checksum; a CRC would neither authenticate a hostile local peer nor add a useful guarantee for the supported transports.

## Packet and payload registries

v1 defines two packet types.

| Value | Name | Required payload format | Meaning |
| ---: | --- | ---: | --- |
| `0x01` | `ApplicationData` | `0x01` (`IpcJson`) | opaque application-protocol bytes |
| `0x02` | `TransportError` | `0x00` (`None`) | structured ITP failure |

Any other packet type is `UnsupportedPacketType`.

`ApplicationData` MUST use a recognized non-`None` application payload format. v1 recognizes only `IpcJson`. A receiver MUST reject an unknown payload format before passing any bytes to the application decoder. A zero-length `ApplicationData` payload is `EmptyApplicationPayload`.

`TransportError` MUST use the `None` payload format. Any other combination is `InvalidFrame`.

## Transport-error payload

A v1 `TransportError` payload has this layout:

| Offset | Size | Field | Encoding |
| ---: | ---: | --- | --- |
| `0` | 2 | error code | unsigned 16-bit big-endian |
| `2` | 2 | message length | unsigned 16-bit big-endian |
| `4` | M | message | UTF-8 diagnostic text |

The UTF-8 message is diagnostic only and MUST NOT be interpreted as a protocol token. Its encoded size is limited to 1024 bytes. Senders truncate longer diagnostics at a valid UTF-8 character boundary.

`message length` MUST equal the exact number of remaining bytes, MUST remain within the 1024-byte limit, and MUST identify valid UTF-8. Violations are `InvalidFrame`.

### Error codes

| Code | Name | Meaning |
| ---: | --- | --- |
| `0x0001` | `InvalidMagic` | stable preamble does not begin with `ITP` |
| `0x0002` | `VersionMismatch` | preamble names an unsupported ITP version |
| `0x0003` | `UnsupportedPacketType` | v1 packet type is unknown |
| `0x0004` | `UnsupportedPayloadFormat` | `ApplicationData` names an unsupported application format |
| `0x0005` | `IncompleteFrame` | EOF occurs before the required frame bytes are complete |
| `0x0006` | `PayloadTooLarge` | declared payload exceeds the configured limit |
| `0x0007` | `InvalidFrame` | another v1 framing invariant is violated |
| `0x0008` | `EmptyApplicationPayload` | `ApplicationData` contains zero payload bytes |

## Error reply rules

A receiver MAY return a `TransportError` only after it has enough valid context to know that the peer speaks v1 and that replying with a v1 frame is safe.

The following failures therefore close the connection without a protocol reply:

- invalid magic;
- unsupported version;
- EOF before the complete v1 header is available.

A failure detected in a recognized incoming `ApplicationData` frame MAY be returned as a structured `TransportError` if the stream remains usable.

An incoming `TransportError` is terminal. A receiver MUST NOT answer it with another `TransportError`, even when the peer's error payload is malformed. A valid peer error is surfaced locally as a peer-reported transport failure; a malformed peer-error payload is a local `InvalidFrame` failure.

## Receiver procedure

A conforming v1 receiver performs these checks in order:

1. read exactly the four-byte stable preamble;
2. validate magic;
3. select the parser for the declared version;
4. read the remaining six v1 header bytes;
5. parse packet type, payload format, and declared length;
6. reject a length above the configured maximum before allocation;
7. validate the packet-type/payload-format combination;
8. read exactly the declared payload bytes;
9. surface `TransportError`, or deliver recognized `ApplicationData` bytes to the application codec.

Application JSON MUST NOT be decoded before the complete ITP frame passes these checks.

## Connection and lifetime rules

The client opens a connection, writes one `ApplicationData` frame, reads one response frame, and closes the connection. The daemon accepts a connection, reads one frame, optionally writes one frame, and closes the connection.

ITP stores no state across connections.

Stream security, when configured, wraps the stream below ITP. ITP does not know whether the underlying bytes are carried by a Unix-domain socket, Windows named pipe, in-process test transport, or TLS-wrapped stream.

## Timeouts and cancellation

ITP has no timeout or cancellation fields.

Connection owners apply a per-operation idle timeout to asynchronous reads and writes. A successful I/O operation starts a fresh idle window for the next operation. This detects a peer that stops making progress.

An independent request deadline bounds the complete exchange, including application processing, and is not reset by partial progress. These deadlines are connection policy supplied by the owning application rather than values negotiated by ITP.

Timeout, cancellation, or EOF before a complete frame is available means no valid frame was received. The connection is abandoned rather than reused.
