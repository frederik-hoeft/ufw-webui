# IPC Transport Protocol (ITP) v1

ITP is the wire-level framing protocol used between `Ufw.Web` and
`Ufw.Systemd`. It is versioned independently of the HTTP API and independently
of the application-level IPC protocol.

ITP exists so a receiving peer can establish wire compatibility before parsing
a version-specific frame, reassemble frames from arbitrary stream fragments,
reject unsafe lengths and unsupported packet metadata before application
decoding, and report recognized v1 framing failures as structured transport
errors.

ITP does not implement protocol negotiation, sessions, multiplexing, request
correlation, or application routing. A connection carries at most one
application exchange.

## Layering

```text
 application protocol bytes
          |
          v
 ITP: [ stable preamble | v1 header | opaque payload ]
          |
          v
 Stream (named pipe / Unix socket / TLS / in-process duplex)
```

For `ApplicationData`, ITP classifies the upper-layer payload format but does
not inspect the payload bytes themselves. The only application format currently
recognized is `IpcJson`.

## Version bootstrap

Every ITP frame begins with a four-byte, version-independent preamble:

```text
 Offset  Size  Field
 0       3     Magic      0x49 0x54 0x50 ("ITP")
 3       1     Version
```

A receiver reads only this preamble before selecting a version-specific parser.
The current wire version is `1`.

If the magic is invalid, the receiver reports `InvalidMagic` locally and closes
the connection. The peer may not speak ITP, so no transport-error frame is
sent.

If the version is unsupported, the receiver reports `VersionMismatch` locally
and closes the connection without parsing any further bytes. It does not assume
that another version understands the v1 header or v1 transport-error format.
This keeps future versions free to change everything after the stable preamble.

There is no version negotiation or fallback handshake.

## Version 1 frame

After the stable preamble, v1 adds a six-byte header followed by the declared
payload. All multi-byte integers are unsigned and big-endian.

```text
 Offset  Size  Field
 0       3     Magic          "ITP"
 3       1     Version        0x01
 4       1     PacketType
 5       1     PayloadFormat
 6       4     PayloadLength  uint32
 10      N     Payload        N = PayloadLength
```

The v1 header is 10 bytes total. There is no trailer, checksum, flags field, or
reserved extension area.

`PayloadLength` is untrusted. It is compared against the configured maximum
before allocating or reading the payload. The default maximum is 16 MiB.
Partial `Stream.Read` results are normal; the receiver accumulates bytes until
each required portion is complete.

ITP relies on the underlying ordered stream for reliable delivery. CRC-32 is
not used because it would not authenticate a hostile local peer and provides no
material protocol guarantee for the supported transports.

## Packet types and payload formats

Two packet types exist:

| Value | Name | Payload format | Payload |
| --- | --- | --- | --- |
| `0x01` | `ApplicationData` | `0x01` (`IpcJson`) | Opaque application-protocol bytes |
| `0x02` | `TransportError` | `0x00` (`None`) | Structured ITP error |

Any other packet type is `UnsupportedPacketType`.

`ApplicationData` must identify a recognized application payload format. An
unknown value is `UnsupportedPayloadFormat`, and the payload body is not read
or passed to the application decoder. An empty `ApplicationData` payload is
`EmptyApplicationPayload`.

`TransportError` is an ITP message rather than application data, so its
`PayloadFormat` must be `None`. Any other value is an invalid v1 frame.

## Transport error payload

The v1 `TransportError` payload is also big-endian:

```text
 Offset  Size  Field
 0       2     ErrorCode      uint16
 2       2     MessageLength  uint16
 4       M     Message        UTF-8, M = MessageLength
```

`Message` is diagnostic only. Receivers do not parse it as a protocol token.
Its encoded UTF-8 form is limited to 1024 bytes; senders truncate longer
diagnostics at a valid character boundary. `MessageLength` must exactly match
the remaining payload length, remain within that bound, and name valid UTF-8.
A violation is `InvalidFrame`.

### Error codes

| Code | Name | Meaning |
| --- | --- | --- |
| `0x0001` | `InvalidMagic` | Stable preamble does not begin with `ITP` |
| `0x0002` | `VersionMismatch` | Stable preamble names an unsupported ITP version |
| `0x0003` | `UnsupportedPacketType` | v1 packet type is unknown |
| `0x0004` | `UnsupportedPayloadFormat` | `ApplicationData` names an unsupported upper-layer format |
| `0x0005` | `IncompleteFrame` | EOF before the required preamble, header, or payload completes |
| `0x0006` | `PayloadTooLarge` | Declared payload exceeds the configured maximum |
| `0x0007` | `InvalidFrame` | Another v1 framing constraint is violated |
| `0x0008` | `EmptyApplicationPayload` | `ApplicationData` declares zero payload bytes |

A locally detected v1 failure is written back as `TransportError` only after
the receiver has enough valid context to know that the peer speaks v1 and that
the incoming packet is not itself a `TransportError`. Preamble failures and an
incomplete v1 header therefore close the connection without a protocol reply.
Failures in a recognized `ApplicationData` frame may return a structured error
when the stream remains usable.

A received `TransportError`, including one with a malformed transport-error
payload, never triggers another `TransportError`. A valid peer error is surfaced
to the local caller as an `ItpException` with `IsPeerReported = true`; malformed
peer-error payloads are local `InvalidFrame` failures. Neither reaches the
application decoder.

## Receiver algorithm

1. Read exactly the four-byte stable preamble, tolerating fragmented reads.
2. Validate `Magic`.
3. Inspect `Version` and select its parser. Unsupported versions stop here.
4. For v1, read exactly the remaining six header bytes.
5. Parse `PacketType`, `PayloadFormat`, and `PayloadLength`.
6. Reject a declared length above the configured maximum before allocating or
   reading the payload.
7. Validate the packet type and its payload-format combination. Unsupported
   application formats stop here, before the body is read.
8. Read exactly `PayloadLength` bytes.
9. Surface `TransportError` as a transport failure, or deliver recognized
   `ApplicationData` bytes to the application codec.

No application JSON parsing occurs until all ITP validation for the frame has
succeeded.

## Timeouts and cancellation

ITP has no timeout field. `TimedStream` applies an idle timeout independently
to each asynchronous read and write operation. Successful I/O starts the next
operation with a fresh idle window, so this bound detects a peer that stops
making progress rather than measuring the total frame duration.

The connection owner also applies an overall request/response deadline around
the transaction. That deadline is not reset by partial reads or writes, so a
peer cannot keep a connection alive indefinitely by trickling bytes within the
I/O timeout. Client cancellation and daemon shutdown cancellation are kept
distinct from expiration of this internal deadline. Either configured timeout
may be explicitly disabled with `Timeout.InfiniteTimeSpan`. A timed-out,
cancelled, or truncated read is not a valid frame and the connection is
abandoned.

## Connection model

The daemon accepts a connection, reads one frame, optionally writes one frame,
and disposes the connection. The client connects, writes one `ApplicationData`
frame, reads one frame, and disposes the connection. A second request uses a
new connection.

ITP stores no session state between connections.

## What ITP is not

ITP is not TLS; stream security wraps the stream below ITP. It is not the
application protocol; methods, routes, statuses, DTO representations, and JSON
semantics live above ITP. It is not a compatibility layer for the old
newline-delimited JSON proof of concept.
