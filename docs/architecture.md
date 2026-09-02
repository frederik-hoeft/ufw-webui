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

`Ufw.Web` exposes versioned rule endpoints (`GET/POST/DELETE /api/v1/rules`) as a JWT-authorized proxy. It does not store firewall state. SQLite is not used as a second source of firewall truth. Mutation bodies are user-signed intents and are forwarded to the daemon without being re-signed or reinterpreted.

## Ufw.Systemd

`Ufw.Systemd` is the privileged daemon. It hosts the IPC request pipeline, routes typed request messages to daemon controllers, and contains the UFW command/interoperability layer.

The transport graph uses the named-pipe transport. On Linux, the configured absolute pipe path is the local Unix-domain IPC endpoint. No TCP transport is present for the daemon.

The daemon exposes unsigned rule listing plus signed `AddRule` and `DeleteRule` mutations. UFW remains the sole source of truth. All UFW process invocations are serialized in-process. Mutations are accepted only after the daemon independently verifies an ECDSA P-256 intent signature against a locally configured authorized-keys file and consumes a persistent nonce.

## IPC layer

`Ufw.Ipc.Client` and `Ufw.Ipc.Shared` define the typed request/response channel
between `Ufw.Web` and `Ufw.Systemd`. The channel has four distinct stages: the
local byte stream, ITP wire framing, the JSON application envelope, and daemon
routing/binding. The detailed protocol contracts live under
[docs/protocols](protocols/README.md).

ITP validates wire compatibility, framing, packet metadata, and bounded payload
lengths before any application JSON is decoded. The application codec then
validates request/response direction, representation semantics, and payload
presence. Only a valid `IRequestMessage` reaches daemon routing, where a matched
endpoint binds the buffered payload to its request DTO before controller code is
invoked. Responses follow the same layers in reverse. Each connection carries one
request/response exchange and holds no reusable protocol session state.

This layering also defines failure containment. Expected peer-originated framing,
application-protocol, transport I/O, timeout, and stream-security failures are
scoped to the current connection so a daemon worker can continue serving later
peers. Transport errors are returned only when enough valid v1 framing context
exists to make a reply safe, and an incoming transport error never triggers a
transport-error loop. Unexpected daemon/framework failures are not classified as
peer failures; they remain observable by faulting the worker/application.

Connection policy applies both a per-I/O idle timeout and an overall transaction
deadline. The idle timeout bounds a read or write that stops making progress; the
transaction deadline bounds the complete exchange even when bytes continue to
arrive slowly. External client cancellation and daemon shutdown remain
cancellation rather than internal timeout failures.

The local IPC channel provides process separation and keeps privileged operations
off the HTTP listener, but reachability is not authorization to mutate firewall
state. The web process must not be able to manufacture an accepted firewall
change without the daemon-side signed-intent verification boundary described in
the security documentation. Filesystem ownership and permissions on the Unix
socket remain defense-in-depth controls for local exposure.

## Firewall state and application metadata

UFW and the daemon remain authoritative for firewall rule existence and semantics. `Ufw.Web` may maintain richer application metadata that UFW itself does not represent, such as display information or authorship history.

Rules are addressed by a content hash of their semantic match/action fields, not by `ufw status numbered` indexes. Display numbers are returned for presentation only. Comments are not part of semantic identity. Unparsed rows (for example IPv6) are still returned so the client can see them, but they cannot be mutated until they can be identified safely.

If richer identifiers are later embedded in UFW comments, the web database may associate metadata with daemon-observed rules. Such metadata must not be treated as evidence that a rule exists. Reconciliation must start from daemon-observed UFW state.

## Request flow

A read-only operation follows this shape:

1. The browser calls the versioned REST API using a JWT access token.
2. `Ufw.Web` authenticates and authorizes the HTTP request.
3. A controller uses `IUfwClient` to issue a typed request over the local Unix IPC endpoint.
4. `Ufw.Systemd` routes the request and reads the authoritative host state.
5. The result returns through IPC and is projected into the HTTP response model.

A firewall mutation follows this shape:

1. A future Blazor client constructs the exact rule specification, canonicalizes it, and signs the intent with the user's ECDSA P-256 private key.
2. The browser calls `POST /api/v1/rules` or `DELETE /api/v1/rules` with a JWT and the signed envelope.
3. `Ufw.Web` authenticates the session and forwards the envelope unchanged over IPC.
4. `Ufw.Systemd` verifies the signature, freshness, nonce, and domain constraints, then executes a validated `ufw` argv array.
5. The result returns through IPC and is projected into the HTTP response.

The security contract is documented in [the security baseline](../security/architecture-baseline.md).

## Extension boundaries

New HTTP controllers belong under versioned API namespaces such as `Api/V1/Controllers`, with public request/response models under the corresponding `Api/V1/Models` area. Application services should remain independent of controller transport concerns.

New daemon operations use the existing typed IPC request/response model and daemon routing infrastructure. Read-only operations may be added independently. Mutating operations require the signed-intent verification boundary described in the security documentation before they can be considered safe to expose.
