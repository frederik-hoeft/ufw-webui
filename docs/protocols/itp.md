# IPC Transport Protocol (ITP) v1

ITP is the wire-level transport protocol used between `Ufw.Web` and
`Ufw.Systemd`. It is versioned independently of the HTTP API and independently
of the application-level IPC protocol.

ITP exists so that a receiving peer can:

- detect that the other side is speaking a different transport version and fail
  without hanging or feeding garbage to the application decoder
- reassemble a complete packet from an arbitrary number of `Stream.Read`
  fragments
- reject untrusted length fields, truncated frames, unknown packet types, and
  corrupted bytes **before** any application-level deserializer runs
- report those failures as structured transport errors rather than as a generic
  I/O failure

ITP does **not** implement protocol negotiation, sessions, multiplexing, or
request/response correlation. A connection carries at most one application
exchange.

## Layering

```
 application JSON  ──────────────────────────────────────────┐
                                                             │
 ITP frame:  [ header | payload | crc32 ]                    │
                                                             ▼
 Stream (named pipe / Unix socket / TLS / in-process duplex)
```

The ITP payload of an `ApplicationData` packet is an opaque byte string. ITP
does not inspect it.

## Versioning

There is no handshake and no version negotiation.

- The current transport version is **1**.
- The version is a single unsigned byte in a fixed location of every frame.
- If the peer's version is not exactly `1`, the receiver must not deliver the
  payload to the application layer. It should emit a `VersionMismatch`
  transport error (if the header can still be skipped using the v1 header
  layout) and close the connection.
- The first 10 bytes of the frame (the header) are **layout-stable** across
  future versions so a v1 peer can skip an unknown-version frame and reply
  with `VersionMismatch` instead of desynchronizing.

Application-level versioning lives in the JSON envelope, not here.

## Frame layout

All multi-byte integers are **unsigned, big-endian**.

```
 Offset  Size  Field
 0       3     Magic          0x49 0x54 0x50   ("ITP")
 3       1     Version        0x01
 4       1     PacketType
 5       1     Flags          must be 0 in v1
 6       4     PayloadLength  uint32
 10      N     Payload        N = PayloadLength
 10+N    4     Crc32          CRC-32 of bytes [0, 10+N)
```

- Header size: 10 bytes
- Trailer size: 4 bytes
- Minimum frame size: 14 bytes (empty payload)
- `PayloadLength` is untrusted. It is compared against a configured maximum
  **before** the payload is allocated or read. The default maximum is
  16 MiB (16 * 1024 * 1024).
- CRC-32 uses the ISO 3309 / ITU-T V.42 polynomial `0xEDB88320` (reflected),
  initial value `0xFFFFFFFF`, final XOR `0xFFFFFFFF`. It covers the header
  **and** the payload, not the trailer itself.

A receiver must accumulate bytes until the header is complete, then until
`PayloadLength + 4` further bytes are complete. Partial `Read` results are
normal and must not be treated as errors.

## Packet types

Only two packet types are defined. There is no keepalive, ping, credit, or
window type.

| Value | Name | Payload |
| --- | --- | --- |
| `0x01` | `ApplicationData` | Opaque application-protocol bytes |
| `0x02` | `TransportError` | Structured ITP error (below) |

Any other type is `UnsupportedPacketType`. The payload of an unknown type
must not be passed to the application decoder.

`Flags` must be zero. A non-zero flags byte is `UnsupportedFlags`. Flags are
reserved so a future version can introduce optional features without moving
header fields; v1 peers reject unknown flags rather than ignoring them.

## Transport error payload

`TransportError` payload, also big-endian:

```
 Offset  Size  Field
 0       2     ErrorCode      uint16
 2       2     MessageLength  uint16
 4       M     Message        UTF-8, M = MessageLength
```

`Message` is diagnostic only. Receivers must not parse it as a protocol
token. `MessageLength` is untrusted and must equal `PayloadLength - 4`. A
malformed transport-error payload is itself an `InvalidFrame`.

### Error codes

| Code | Name | When |
| --- | --- | --- |
| `0x0001` | `InvalidMagic` | Bytes 0–2 are not `ITP` |
| `0x0002` | `VersionMismatch` | Version byte is not `1` |
| `0x0003` | `UnsupportedPacketType` | Packet type is not `0x01` or `0x02` |
| `0x0004` | `UnsupportedFlags` | Flags byte is not `0` |
| `0x0005` | `IncompleteFrame` | EOF before the declared frame was fully received |
| `0x0006` | `InvalidChecksum` | CRC-32 mismatch (garbled header or payload) |
| `0x0007` | `PayloadTooLarge` | Declared length exceeds the configured maximum |
| `0x0008` | `InvalidFrame` | Any other framing violation (including a malformed error payload) |
| `0x0009` | `EmptyApplicationPayload` | `ApplicationData` with length 0 (nothing for the app layer to parse) |

`InvalidMagic` is not written back to the peer: the peer may not be speaking
ITP at all. Every other locally detected error should be written as a
`TransportError` packet when the stream is still writable, then the
connection is closed.

A `TransportError` received from the peer is surfaced to the local caller as
a structured failure (`ItpException` with `IsPeerReported = true`). It is
never decoded as application data.

## Receiver algorithm

1. Read exactly 10 header bytes, handling short reads. EOF before any header
   byte, or in the middle of the header, is `IncompleteFrame`.
2. If magic ≠ `ITP`, fail with `InvalidMagic`. Do not write an error packet.
3. Read `PacketType`, `Flags`, `PayloadLength`.
4. If `PayloadLength` > configured maximum, fail with `PayloadTooLarge`.
   Do **not** read the declared payload.
5. Read exactly `PayloadLength + 4` further bytes. EOF is `IncompleteFrame`.
6. Verify CRC-32. On mismatch: `InvalidChecksum`. Stop. The payload is
   untrusted and must not be given to the application decoder.
7. If `Version ≠ 1`: `VersionMismatch`. Do not inspect the payload.
8. If `Flags ≠ 0`: `UnsupportedFlags`.
9. If `PacketType` is unknown: `UnsupportedPacketType`.
10. If `PacketType == TransportError`, parse the error payload and surface
    it as a transport failure.
11. If `PacketType == ApplicationData` and `PayloadLength == 0`:
    `EmptyApplicationPayload`.
12. Otherwise deliver the payload bytes to the application protocol decoder.

Steps 7–11 occur only after the frame has been fully framed and
integrity-checked. A garbled packet therefore cannot reach the application
deserializer.

## Timeouts and cancellation

ITP itself has no timeout field. Read and write loops honor:

- the caller `CancellationToken` (host shutdown, test cancellation, user
  abort)
- the connection I/O timeout applied by `TimedStream` (daemon
  `Network.RequestTimeout`, client `UfwClientOptions.RequestTimeout`)

A timed-out or cancelled read is not a valid frame. The connection is
abandoned; the peer must not be left blocked forever.

## Connection model

- No session state is stored at the ITP layer.
- The daemon accepts a connection, reads one frame, optionally writes one
  frame, and disposes the connection.
- The client connects, writes one `ApplicationData` frame, reads one frame,
  and disposes the connection.
- A second application request always uses a new connection.

## What ITP is not

- It is not TLS. Stream security, if any, wraps the stream *below* ITP.
- It is not the application protocol. Status codes, routes, methods, and
  payload kinds live above ITP.
- It is not a compatibility shim for the previous `\n`-delimited JSON POC.
