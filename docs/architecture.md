# Architecture

## System model

UFW WebUI is split into a network-facing application and a privileged host daemon. The split limits the code that can directly affect the host firewall and keeps the privileged daemon off the network-facing HTTP surface.

The three principal participants are:

1. The browser frontend presents management state and obtains user authorization for actions. A Blazor frontend is planned, but frontend implementation is outside the current codebase scope.
2. `Ufw.Web` exposes the HTTP API, manages users and web sessions, stores application metadata, and mediates access to the daemon through local IPC.
3. `Ufw.Systemd` owns the host-facing firewall integration. It is the authoritative source for UFW state and is the only component that may execute privileged UFW operations.

The web application is not an authority for firewall state merely because it can reach the daemon. Its database is application state, not a second firewall database.

## Ufw.Web

`Ufw.Web` is an ASP.NET Core controller application. Its current responsibilities are infrastructure rather than firewall feature endpoints:

- ASP.NET Core Identity backed by EF Core and SQLite
- JWT bearer authentication for API requests
- opaque refresh-token issuance, rotation, and revocation
- API versioning and controller discovery
- CORS configuration for a browser frontend
- development Swagger UI and a health endpoint
- local IPC client registration for future daemon-backed controllers

The web database stores Identity data and refresh-token state. Refresh tokens are random opaque values delivered in an `HttpOnly`, `Secure`, `SameSite=Strict` cookie. Only SHA-256 token hashes are persisted. Refresh tokens rotate on use, belong to a token family, and family reuse invalidates remaining active tokens. A stored Identity security stamp ties a refresh-token family to the user's current security state.

Access tokens are short-lived RSA-signed JWTs. They authorize access to the HTTP API; they are not proof that a firewall mutation was approved by a user for daemon execution.

There is no rule CRUD controller or firewall-state entity in `Ufw.Web`. SQLite is not used as a second source of firewall truth.

## Ufw.Systemd

`Ufw.Systemd` is the privileged daemon. It hosts the IPC request pipeline, routes typed request messages to daemon controllers, and contains the UFW command/interoperability layer.

The transport graph uses the named-pipe transport. On Linux, the configured absolute pipe path is the local Unix-domain IPC endpoint. No TCP transport is present for the daemon.

The daemon currently exposes only the rule-listing placeholder. No mutating daemon endpoint is present. This is intentional: a mutation path must not be introduced until daemon-side verification of user-signed mutation intents is available.

## IPC layer

`Ufw.Ipc.Client` and `Ufw.Ipc.Shared` provide the typed request/response protocol used between `Ufw.Web` and `Ufw.Systemd`. The on-wire stack is two independently versioned layers: the binary [ITP](protocols/itp.md) transport and the JSON [application protocol](protocols/application-protocol.md). Routing and controller shape are unchanged.

The local IPC channel provides process separation and keeps privileged operations off the HTTP listener. It does not make `Ufw.Web` trusted to authorize firewall mutations. Any process that gains the web application's effective capabilities must still be unable to manufacture an accepted firewall change without a valid user signature.

The current transport-security abstraction is not part of the mutation authorization model. The web and daemon presently use the no-op stream-security implementation over the local pipe. Filesystem ownership/permissions for the Unix socket remain an operational control for limiting local access, but they are defense in depth rather than proof of user intent.

Daemon workers isolate expected peer/connection failures from the serving loop. ITP framing failures (bad magic, truncated frames, version mismatch, unknown packet type, unsupported application payload format), application-document errors, transport I/O failures, I/O timeouts, and TLS-authentication failures terminate the current connection and are logged, after which the worker accepts another request. ITP-invalid frames are never passed to the application decoder, and incoming transport-error packets are terminal one-way notifications rather than requests that can trigger another transport error. Application-protocol failures remain distinct from raw transport I/O, while unexpected daemon/framework exceptions are not swallowed and fault the worker/application so they remain observable.

Both sides enforce two independent time bounds around an IPC exchange. `TimedStream` applies an idle timeout to each asynchronous read and write operation, so a peer that stops making I/O progress releases the connection. A separate request timeout bounds the complete transaction even when the peer continues to trickle data within the idle limit. On the client the transaction covers connect, stream-security setup, request write, response read, and response handling; on the daemon it starts after accept and covers stream-security setup, request read/processing, and response write. External client cancellation and daemon shutdown cancellation remain cancellation rather than being reported as timeouts. Either timeout can be explicitly disabled with `Timeout.InfiniteTimeSpan`. The client and daemon each use a single request/response exchange per connection; there is no ITP session.

## Firewall state and application metadata

UFW and the daemon remain authoritative for firewall rule existence and semantics. `Ufw.Web` may maintain richer application metadata that UFW itself does not represent, such as display information or authorship history.

If stable rule identifiers can be embedded in UFW comments, the web database may use those identifiers to associate metadata with daemon-owned rules. Such metadata must not be interpreted as authoritative evidence that a rule exists, is enabled, or has particular firewall semantics. Reconciliation must start from daemon-observed UFW state.

## Request flow

A read-only operation follows this shape:

1. The browser calls the versioned REST API using a JWT access token.
2. `Ufw.Web` authenticates and authorizes the HTTP request.
3. A controller uses `IUfwClient` to issue a typed request over the local Unix IPC endpoint.
4. `Ufw.Systemd` routes the request and reads the authoritative host state.
5. The result returns through IPC and is projected into the HTTP response model.

A firewall mutation is not implemented in the current system. Its required security contract is documented in [the security baseline](../security/architecture-baseline.md).

## Extension boundaries

New HTTP controllers belong under versioned API namespaces such as `Api/V1/Controllers`, with public request/response models under the corresponding `Api/V1/Models` area. Application services should remain independent of controller transport concerns.

New daemon operations use the existing typed IPC request/response model and daemon routing infrastructure. Read-only operations may be added independently. Mutating operations require the signed-intent verification boundary described in the security documentation before they can be considered safe to expose.
