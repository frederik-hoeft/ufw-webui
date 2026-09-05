# Architecture

## System model

UFW WebUI separates network-facing application concerns from privileged host firewall execution. The split limits the code that can directly affect the firewall while still allowing an HTTP-facing application to expose management workflows.

The system has three principal participants:

1. `Ufw.Client` is the Blazor WebAssembly browser frontend. It presents firewall state, manages browser-local authentication state, and obtains user authorization for signed mutations. Other API clients may use the same REST and signed-intent contracts.
2. `Ufw.Web` exposes the REST API, manages users and web sessions, and proxies daemon-backed operations over local IPC.
3. `Ufw.Systemd` owns the host-facing UFW integration. It is authoritative for firewall state and is the only component that executes privileged UFW commands.

`Ufw.Web` is deliberately not part of the privileged trust boundary. Its database contains application and authentication state, not a second copy of firewall state, and the ability to reach the daemon is not sufficient authority to mutate UFW.

## Ufw.Client

`Ufw.Client` is a standalone Blazor WebAssembly application using MudBlazor. It depends on the versioned HTTP API rather than on daemon transport details. UI components delegate authentication, REST access, and intent signing to scoped services so presentation code does not construct authorization headers or security envelopes directly.

The client uses MudBlazor's default light and dark palettes and exposes that choice as a global UI preference. The selected light/dark mode is the only non-sensitive client preference intentionally stored in browser local storage; authentication state, refresh tokens, and mutation private keys are not persisted there.

Short-lived access JWTs are held only in memory. On application startup, shortly before an access token expires, and once after an authenticated API request rejects the current access token, the client asks `Ufw.Web` to rotate the secure `HttpOnly` refresh cookie and issue a new access token. The rejected request is replayed at most once with the replacement token. Login, refresh, and logout all mutate the same browser cookie, so the client serializes those operations within the tab and across same-origin tabs before calling the authentication API. Cross-origin API calls include browser credentials so the cookie participates in those operations without becoming visible to application JavaScript. The refresh cookie is `Secure` and `SameSite=Strict`, so the client must be served over HTTPS and from a site compatible with the API cookie. Web Locks are origin-scoped; deployments therefore use one consistent client origin for browser tabs that share the API refresh cookie rather than exposing the same session through multiple independently locking client origins.

Client startup treats authentication restoration as an explicit state transition. A missing or rejected refresh session produces the normal anonymous state, while transport failures, server failures, malformed successful responses, and protocol incompatibilities keep the application in a retryable initialization-failure state instead of presenting a misleading login screen. Expected client failures are classified centrally into stable user-facing messages; successful HTTP responses that violate the expected JSON/protocol contract become protocol errors. Unexpected component failures are contained by a route-level error boundary and require an explicit reload rather than replacing the entire application with raw exception output.

For firewall mutations the client reuses the rule validation, normalization, and canonical intent implementation from `Ufw.Ipc.Shared`, then performs P-256 SHA-256 signing through the browser Web Crypto API. Intent timestamps and access-token freshness decisions both use the injected `TimeProvider`, keeping time-dependent client behavior deterministic outside the browser wall clock. Shared validation gives the editor the same address, port, interface, and comment semantics enforced by the daemon, but remains a usability check only: the daemon independently validates every signed payload before authorization and execution. The initial UX accepts an unencrypted PKCS#8 private key in a masked input for each individual AddRule or DeleteRule request. The preferred representation is the single-line `data:application/pkcs8;base64,...` form, while PKCS#8 PEM and raw base64 DER remain accepted. The input is cleared after the attempt and is never placed in browser storage or application-wide state. This is an intentionally temporary key-entry model; persistent or hardware-backed key handling requires a separate security design.

The rules view tracks daemon responses as explicit authoritative snapshots rather than treating default UI values as firewall state. Before the first successful read, no active/inactive or empty-rule state is inferred. A failed refresh keeps the last successfully loaded snapshot visible as stale but disables further mutations until a fresh daemon read succeeds. The same stale-state boundary is entered when a mutation succeeds but post-mutation reconciliation fails, when the client cannot determine whether a mutation completed, or when the daemon rejects a mutation in a way that requires the client to re-read current state.

## Ufw.Web

`Ufw.Web` is an ASP.NET Core controller application. Its responsibilities include:

- ASP.NET Core Identity backed by EF Core and SQLite;
- JWT bearer authentication for API requests;
- opaque refresh-token issuance, rotation, and revocation;
- API versioning, controller discovery, CORS, and development Swagger support;
- JWT-protected rule and intent-context REST endpoints;
- local IPC client registration and daemon-response projection.

`Ufw.Web` treats `src/Ufw.Web/appsettings.json` as its only local JSON configuration source. The committed `appsettings.default.json` is a template rather than an additional runtime layer; environment variables and command-line arguments override the local file for containerized and other externalized deployments. Environment-specific appsettings files and ASP.NET Core user secrets are intentionally outside this configuration model.

Refresh tokens are random opaque values delivered in an `HttpOnly`, `Secure`, `SameSite=Strict` cookie. Only SHA-256 token hashes are persisted. Refresh tokens rotate on use, belong to a token family, and family reuse invalidates remaining active tokens. A stored Identity security stamp ties a refresh-token family to the user's current security state.

Initial Identity accounts may be supplied through `Auth:Bootstrap:Users` from any normal ASP.NET Core configuration provider, including environment variables used by container deployments. Bootstrap runs after database migration and is deliberately non-destructive: it creates missing users through `UserManager`, reconciles configured email-confirmation state, never resets an existing password, and never deletes users that disappear from configuration. A password is therefore creation-only bootstrap material rather than ongoing desired-state configuration.

Access tokens are short-lived P-256 ECDSA JWTs signed with ES256. They authorize access to the HTTP API; they are not proof that a firewall mutation was approved for daemon execution.

The current daemon-backed REST surface includes:

- `GET /api/v1/intent/context`, which returns the signed-intent protocol version and daemon deployment identifier;
- `GET /api/v1/rules`, which returns authoritative rule state without requiring a mutation signature;
- `POST /api/v1/rules`, which forwards a signed `rules.add` intent;
- `DELETE /api/v1/rules`, which forwards a signed `rules.delete` intent.

Mutation envelopes are forwarded without being re-signed or replaced by ASP-owned firewall state. Application-level authorization can restrict which HTTP requests a legitimate session may submit, but the daemon independently establishes mutation authority.

## Ufw.Systemd

`Ufw.Systemd` is the privileged daemon. It hosts the IPC request pipeline, source-generated daemon routing, signed-intent authorization, rule parsing and normalization, and the UFW subprocess boundary.

The daemon exposes read-only rule and intent-context operations plus signed AddRule and DeleteRule mutations. UFW remains the sole source of truth for rule existence and semantics, including rules created or modified outside this application.

All UFW subprocess activity is serialized through one in-process execution gate. A mutation keeps that gate through authorization replay consumption, current-state checks, subprocess completion, and post-mutation reconciliation. Once a mutating child process starts, cancellation does not release ownership of that child: it is terminated and reaped if required, authoritative state is reconciled, and only then is cancellation propagated to the caller.

UFW is invoked directly with validated argv elements rather than through a shell. The process environment forces a deterministic locale, stdout is reserved for parseable UFW output, and stderr is retained as diagnostics. A successful mutation response is returned only after the expected authoritative post-operation state has been observed.

## Development UFW substitute

`Ufw.Mock` is a development-only executable that implements the UFW 0.36.2 command boundary without interacting with netfilter or requiring elevated privileges. It is intentionally outside the production trust path: no production project depends on it, and `Ufw.Systemd` does not contain mock-specific execution logic. Development configurations substitute only the executable selected by `ufw_path`.

The daemon therefore continues to construct the same argv, serialize access through the same execution gate, parse the same `ufw status numbered` representation, and perform the same post-mutation reconciliation. The mock persists enabled state, default policies, logging configuration, application profiles, and concrete IPv4/IPv6 rules in a local JSON state file. `UFW_MOCK_STATE_PATH` can isolate that state for tests or parallel development environments.

The mock reuses `Ufw.Ipc.Shared` for normalized firewall-rule semantics, but keeps UFW-specific CLI grammar, extended protocol names, per-rule logging, application-profile handling, persistence, and output formatting local to the development tool. This avoids widening the production semantic contract solely for mock compatibility.

Compatibility is defined at the observable UFW CLI boundary rather than at Linux kernel internals. Rule ordering, global numbered insertion, family-specific deletion, IPv4/IPv6 materialization, status formatting, lifecycle/default/logging commands, and documented rule syntax are modeled because daemon and manual-development flows can depend on them. Host-dependent reports such as raw netfilter tables and listening sockets are deterministic synthetic reports so the mock remains platform-neutral and cannot affect or inspect the host firewall.

## IPC layer

`Ufw.Ipc.Client` and `Ufw.Ipc.Shared` define the typed request/response channel between `Ufw.Web` and `Ufw.Systemd`. The channel has four distinct stages: the local byte stream, ITP wire framing, the JSON application envelope, and daemon routing/binding. The detailed protocol contracts live under [docs/protocols](protocols/README.md).

ITP validates wire compatibility, framing, packet metadata, and bounded payload lengths before any application JSON is decoded. The application codec then validates request/response direction, representation semantics, and payload presence. Only a valid `IRequestMessage` reaches daemon routing, where a matched endpoint binds the buffered payload to its request DTO before controller code is invoked. Responses follow the same layers in reverse. Each connection carries one request/response exchange and holds no reusable protocol session state.

This layering also defines failure containment. Expected peer-originated framing, application-protocol, transport I/O, timeout, and stream-security failures are scoped to the current connection so a daemon worker can continue serving later peers. Transport errors are returned only when enough valid v1 framing context exists to make a reply safe, and an incoming transport error never triggers a transport-error loop. Unexpected daemon/framework failures are not classified as peer failures; they remain observable by faulting the worker/application.

Connection policy applies both a per-I/O idle timeout and an overall transaction deadline. The idle timeout bounds a read or write that stops making progress; the transaction deadline bounds the complete exchange even when bytes continue to arrive slowly. External client cancellation and daemon shutdown remain cancellation rather than internal timeout failures.

The local IPC endpoint is a named pipe on Windows and a Unix-domain socket path on Linux. TLS is optional and configured independently from protocol selection. When TLS is enabled, `SslProtocols.None` keeps its standard .NET meaning and lets the runtime/OS negotiate the supported protocol set; deployments may instead select explicit protocols. The server is authenticated whenever TLS is enabled. Client-certificate validation can additionally enable mTLS. TLS and socket permissions are defense in depth and do not replace signed-intent authorization.

## Firewall rule model and identity

Rule listing translates supported `ufw status numbered` rows into a normalized semantic model. That model is shared by display, identity, signed mutation payloads, duplicate detection, and UFW argument construction so the meaning that is signed is the meaning the daemon executes.

The semantic model covers:

- action (`allow`, `deny`, `reject`, or `limit`);
- address family (`IPv4`, `IPv6`, or family-neutral for AddRule input);
- direction (`in`, `out`, or `forward`);
- protocol;
- source and destination addresses/CIDRs and ports;
- interfaces whose meaning depends on direction;
- an optional comment.

Normalization is semantic rather than textual. IPv4 and IPv6 CIDRs are reduced to their canonical network address, equivalent all-addresses forms normalize to `any`, and port sets are sorted, deduplicated, and merged where ranges overlap or are adjacent. For non-forward rules, only the interface meaningful for that direction is accepted; forward rules may carry both ingress and egress interfaces.

Rule identity is a SHA-256 content hash of normalized match/action semantics. Comments and current UFW row numbers are deliberately excluded. IPv4 and IPv6 are distinct semantic identities. A family-neutral AddRule can correspond to the concrete IPv4 and IPv6 rows that UFW materializes, whereas DeleteRule always targets a concrete family-specific identity returned by listing.

The current number from `ufw status numbered` is returned only as display/current-execution information. DeleteRule carries the semantic identity plus its complete rule specification. The daemon re-reads UFW while holding the execution gate and resolves that semantic identity to the current row immediately before deletion. Missing or ambiguous identities are rejected instead of deleting whatever happens to occupy an older row number.

Only rows that are completely parsed and pass the same semantic validation used for mutations receive a `ruleId`. Unsupported or malformed rows remain visible in the read model as raw, unaddressable state. This preserves visibility without turning a partial parser interpretation into mutation authority.

## Request flows

### Rule listing

A read-only rule operation follows this path:

1. The client calls the versioned REST API using a JWT access token.
2. `Ufw.Web` authenticates and authorizes the HTTP request and issues a typed IPC request.
3. `Ufw.Systemd` serializes access to UFW, executes `ufw status numbered` under a deterministic locale, and parses stdout.
4. Supported rows are normalized and assigned semantic identities; unsupported rows remain visible without mutable identities.
5. The authoritative result returns through IPC and is projected into the HTTP response.

### Firewall mutation

A signed mutation follows this path:

1. The client obtains `GET /api/v1/intent/context` and uses its protocol version and deployment identifier when constructing the intent.
2. The client normalizes the exact rule specification, creates the AddRule or DeleteRule payload, and signs the canonical intent with an authorized ECDSA P-256 private key.
3. `Ufw.Web` authenticates the HTTP session and forwards the signed envelope over IPC.
4. `Ufw.Systemd` validates deployment scope, operation, payload semantics, rule identity where applicable, signature, and freshness against daemon-owned trust/configuration.
5. Under the UFW execution gate, the daemon durably consumes the nonce before any mutation can execute, reads current authoritative state, and applies duplicate/target-resolution checks.
6. The daemon constructs validated UFW argv, executes the child process, and retains process ownership through exit or cancellation cleanup.
7. The daemon re-reads UFW and returns success only if the expected authoritative state is confirmed.

The cryptographic and replay invariants are described in [the security baseline](../security/architecture-baseline.md). The exact signed representation is defined in [the signed-intent protocol](protocols/signed-intent.md).

## Firewall state and application metadata

UFW and `Ufw.Systemd` are authoritative for firewall rule existence and semantics. `Ufw.Web` may later maintain richer application metadata such as presentation information, authorship, semantic analysis, or reachability analysis, but that state cannot establish that a firewall rule exists or authorize a mutation.

Out-of-band UFW changes are expected. Rule listing observes them directly, semantic identity allows manually created supported rules to be addressed, and DeleteRule resolves against fresh daemon-observed state. If future web metadata and UFW disagree, reconciliation starts from UFW.

## Extension boundaries

New HTTP controllers belong under versioned API namespaces such as `Api/V1/Controllers`, with public request/response models under the corresponding API model area. Application services should remain independent of controller transport concerns.

New daemon operations use the typed IPC request/response and source-generated routing infrastructure. Read-only operations may remain unsigned when their data sensitivity permits. Mutating operations must reuse the signed-intent authorization boundary rather than treating IPC peer identity or ASP authorization as mutation authority.

New rule syntax is safe to expose for mutation only when parsing, normalization, validation, semantic identity, signed canonicalization, and UFW argument construction agree on its meaning. Unsupported UFW output should remain visible but unaddressable until that complete semantic path exists.
