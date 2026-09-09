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

The client defines coordinated light and dark MudBlazor palettes and exposes that choice as a global UI preference. Its UI typography self-hosts the IBM Plex Sans and IBM Plex Mono weights used by the design, with system fallback stacks retained for resilience and `font-display: swap` so font loading does not block first paint. Appearance and language are non-sensitive browser-local preferences and are persisted in local storage. Runtime .NET code reaches Web Storage through the injected `ILocalStorage` boundary rather than issuing storage-specific JavaScript calls from feature services. Authentication state, refresh tokens, and mutation private keys are not persisted there.

Short-lived access JWTs are held only in memory. On application startup, shortly before an access token expires, and once after an authenticated API request rejects the current access token, the client asks `Ufw.Web` to rotate the secure `HttpOnly` refresh cookie and issue a new access token. The rejected request is replayed at most once with the replacement token. Login, refresh, and logout all mutate the same browser cookie, so the client serializes those operations within the tab and across same-origin tabs before calling the authentication API. Cross-origin API calls include browser credentials so the cookie participates in those operations without becoming visible to application JavaScript. The refresh cookie is `Secure` and `SameSite=Strict`, so the client must be served over HTTPS and from a site compatible with the API cookie. Web Locks are origin-scoped; deployments therefore use one consistent client origin for browser tabs that share the API refresh cookie rather than exposing the same session through multiple independently locking client origins.

Client startup treats authentication restoration as an explicit state transition. A missing or rejected refresh session produces the normal anonymous state, while transport failures, server failures, malformed successful responses, and protocol incompatibilities keep the application in a retryable initialization-failure state instead of presenting a misleading login screen. Expected client failures are classified centrally into stable user-facing messages; successful HTTP responses that violate the expected JSON/protocol contract become protocol errors. API failures are logged with their HTTP method, request path, and status while unexpected .NET failures receive a short diagnostic reference that is shown to the user and logged with the original exception. The pre-Blazor bootstrap applies the same reference pattern to unhandled browser/startup errors. Unexpected component failures remain contained by a route-level error boundary and require an explicit reload rather than replacing the application with raw exception output.

For firewall mutations the client reuses rule validation/normalization from `Ufw.Shared.Firewall` and canonical intent handling from `Ufw.Shared.Security.Intent`, then performs P-256 SHA-256 signing through the browser Web Crypto API. Intent timestamps and access-token freshness decisions both use the injected `TimeProvider`, keeping time-dependent client behavior deterministic outside the browser wall clock. Shared validation gives the editor the same address, port, interface, and comment semantics enforced by the daemon, but remains a usability check only: the daemon independently validates every signed payload before authorization and execution. The `Ufw.Shared.Firewall.Rendering` service renders the same validated rule fields into canonical UFW rule syntax for mutation confirmation UI and daemon argv construction. The displayed command is informational and is not part of the current signed-intent payload; the structural rule remains authoritative. The initial UX accepts an unencrypted PKCS#8 private key in a masked input for each individual AddRule or DeleteRule request. The preferred representation is the single-line `data:application/pkcs8;base64,...` form, while PKCS#8 PEM and raw base64 DER remain accepted. The input is cleared after the attempt and is never placed in browser storage or application-wide state. This is an intentionally temporary key-entry model; persistent or hardware-backed key handling requires a separate security design.

The rules view tracks daemon responses as explicit authoritative snapshots rather than treating default UI values as firewall state. Before the first successful read, no active/inactive or empty-rule state is inferred. A failed refresh keeps the last successfully loaded snapshot visible as stale but disables further mutations until a fresh daemon read succeeds. The same stale-state boundary is entered when a mutation succeeds but post-mutation reconciliation fails, when the client cannot determine whether a mutation completed, or when the daemon rejects a mutation in a way that requires the client to re-read current state.

Operational status reuses the existing authenticated daemon-backed intent-context and rule-list reads instead of introducing a parallel health protocol. A successful status check therefore proves that the browser can complete that request path through `Ufw.Web` to `Ufw.Systemd`, while failures remain deliberately unattributed because the current REST API cannot distinguish browser-to-ASP failure from ASP-to-daemon failure. The status surface also reports firewall active state, rule count, daemon deployment identity, and signed-intent protocol compatibility from those authoritative responses. Navigation exposes the last completed daemon-backed availability check as a compact connection indicator rather than fabricating process state.

Settings are limited to preferences and management surfaces that already have a real client-side contract. Appearance and language use browser-local preference services, and host interface management links to the advisory interface inventory workflow. Server- or daemon-owned configuration is not represented as editable UI until a corresponding backend ownership and persistence contract exists.

Localization is a client presentation concern rather than part of REST, IPC, firewall, or signed-intent semantics. Supported cultures and the default culture are configured in the client application settings. A small bootstrap script resolves the persisted culture before starting Blazor and supplies it as the WebAssembly application culture, which prevents the first rendered component tree from briefly using the fallback language. Because that bootstrap executes before the .NET dependency-injection container exists, it is the deliberate exception to the runtime `ILocalStorage` boundary. The script owns only culture selection plus pre-Blazor loading/fatal-error text; normal application strings are grouped by feature in .NET localization resources and resolved through injected localizers. Neutral English resources provide the fallback, while additional cultures can be added without changing application logic.

Culture-sensitive presentation uses the active UI culture for human-readable dates, times, counts, and durations. Canonical UFW command text, protocol names and versions, rule identifiers, interface names, signed payloads, and other machine-facing values remain culture-invariant. Problem details supplied by the server are treated as server-owned text; the client localizes its own stable error categories without rewriting server diagnostics.

## Ufw.Web

`Ufw.Web` is an ASP.NET Core controller application. Its responsibilities include:

- ASP.NET Core Identity backed by EF Core and PostgreSQL;
- JWT bearer authentication for API requests;
- opaque refresh-token issuance, rotation, and revocation;
- API versioning, controller discovery, CORS, and development Swagger support;
- JWT-protected rule and intent-context REST endpoints;
- local IPC client registration and daemon-response projection.

`Ufw.Web` treats `src/Ufw.Web/appsettings.json` as its only local JSON configuration source. The committed `appsettings.default.json` is a template rather than an additional runtime layer; environment variables and command-line arguments override the local file for containerized and other externalized deployments. Environment-specific appsettings files and ASP.NET Core user secrets are intentionally outside this configuration model.

Persistent web state is stored in PostgreSQL. `ApplicationDbContext` keeps ASP.NET Core Identity's built-in model and loads application-owned entity mappings through the source-generated `Wkg.EntityFrameworkCore` model loader. Application entities keep their mapping beside the entity as discoverable model configuration rather than accumulating mapping logic in the context. The loader audits application-owned entities with explicit entity-naming and property-mapping policies, so incomplete mappings fail model construction instead of silently relying on EF conventions. EF migrations remain the schema source of truth and are applied before authentication bootstrap runs.

Database-backed request work uses `Wkg.AspNetCore` request-scoped transactions. The authentication controller opens the transaction before Identity or refresh-token services touch the shared scoped `ApplicationDbContext`, and returns explicit commit/rollback continuations. Identity stores and application services resolve that same scoped context, so their writes participate in one request transaction rather than committing independently. Expected writes are committed even when the HTTP result is an authentication failure, for example failed-login lockout counters and refresh-family invalidation. Unexpected exceptions flow through the WKG error sentry and force rollback. The default isolation level is `ReadCommitted`; refresh-token replay safety is enforced with the token row's optimistic concurrency token, and a losing rotation can then observe the winning commit and revoke active family members with a set-based update in the same request transaction.

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

UFW is invoked directly with validated argv elements rather than through a shell. Rule-expression argv is rendered by the injected `Ufw.Shared.Firewall.Rendering` command renderer from the validated structural rule; the daemon prepends execution-only options such as `--force` and passes the resulting argv directly to the child process. The same renderer supplies the human-readable rule syntax shown by the browser, so confirmation text and executed rule semantics do not maintain independent formatting implementations. The process environment forces a deterministic locale, stdout is reserved for parseable UFW output, and stderr is retained as diagnostics. A successful mutation response is returned only after the expected authoritative post-operation state has been observed.

## Development UFW substitute

`Ufw.Mock` is a development-only executable that implements the UFW 0.36.2 command boundary without interacting with netfilter or requiring elevated privileges. It is intentionally outside the production trust path: no production project depends on it, and `Ufw.Systemd` does not contain mock-specific execution logic. Development configurations substitute only the executable selected by `ufw_path`.

The daemon therefore continues to construct the same argv, serialize access through the same execution gate, parse the same `ufw status numbered` representation, and perform the same post-mutation reconciliation. The mock persists enabled state, default policies, logging configuration, application profiles, and concrete IPv4/IPv6 rules in a local JSON state file. `UFW_MOCK_STATE_PATH` can isolate that state for tests or parallel development environments.

The mock reuses `Ufw.Shared.Firewall` for normalized firewall-rule semantics, but keeps UFW-specific CLI grammar, extended protocol names, per-rule logging, application-profile handling, persistence, and output formatting local to the development tool. This avoids widening the production semantic contract solely for mock compatibility.

Compatibility is defined at the observable UFW CLI boundary rather than at Linux kernel internals. Rule ordering, global numbered insertion, family-specific deletion, IPv4/IPv6 materialization, status formatting, lifecycle/default/logging commands, and documented rule syntax are modeled because daemon and manual-development flows can depend on them. Host-dependent reports such as raw netfilter tables and listening sockets are deterministic synthetic reports so the mock remains platform-neutral and cannot affect or inspect the host firewall.

## IPC layer

`Ufw.Ipc.Client` and the `Ufw.Shared.Ipc` namespaces define the typed request/response channel between `Ufw.Web` and `Ufw.Systemd`. `Ufw.Shared` also contains cross-process concepts that are deliberately independent of IPC, including firewall semantics/rendering, signed-intent primitives, certificate helpers, and threading utilities. The channel has four distinct stages: the local byte stream, ITP wire framing, the JSON application envelope, and daemon routing/binding. The detailed protocol contracts live under [docs/protocols](protocols/README.md).

ITP validates wire compatibility, framing, packet metadata, and bounded payload lengths before any application JSON is decoded. The application codec then validates request/response direction, representation semantics, and payload presence. Only a valid `IRequestMessage` reaches daemon routing, where a matched endpoint binds the buffered payload to its request DTO before controller code is invoked. Responses follow the same layers in reverse. Each connection carries one request/response exchange and holds no reusable protocol session state.

This layering also defines failure containment. Expected peer-originated framing, application-protocol, transport I/O, timeout, and stream-security failures are scoped to the current connection so a daemon worker can continue serving later peers. Transport errors are returned only when enough valid v1 framing context exists to make a reply safe, and an incoming transport error never triggers a transport-error loop. Unexpected daemon/framework failures are not classified as peer failures; they remain observable by faulting the worker/application.

Connection policy applies both a per-I/O idle timeout and an overall transaction deadline. The idle timeout bounds a read or write that stops making progress; the transaction deadline bounds the complete exchange even when bytes continue to arrive slowly. External client cancellation and daemon shutdown remain cancellation rather than internal timeout failures.

The local IPC endpoint is a named pipe on Windows and a Unix-domain socket path on Linux. TLS is optional and configured independently from protocol selection. When TLS is enabled, `SslProtocols.None` keeps its standard .NET meaning and lets the runtime/OS negotiate the supported protocol set; deployments may instead select explicit protocols. The server is authenticated whenever TLS is enabled. Client-certificate validation can additionally enable mTLS. TLS and socket permissions are defense in depth and do not replace signed-intent authorization.

## Firewall rule model and identity

Rule listing translates supported `ufw status numbered` rows into a normalized semantic model. That model is shared by display, identity, signed mutation payloads, duplicate detection, and UFW rule rendering so the meaning that is signed is the meaning the daemon executes. Canonical UFW presentation is derived from the same renderer that produces daemon rule argv, but that presentation string is not itself an authorization input.

The semantic model covers:

- action (`allow`, `deny`, `reject`, or `limit`);
- address family (`IPv4`, `IPv6`, or family-neutral for AddRule input);
- direction (`in`, `out`, or `forward`);
- protocol;
- source and destination addresses/CIDRs and ports;
- interfaces whose meaning depends on direction;
- an optional comment.

Normalization is semantic rather than textual. IPv4 and IPv6 CIDRs are reduced to their canonical network address, equivalent all-addresses forms normalize to `any`, and port sets are sorted, deduplicated, and merged where ranges overlap or are adjacent. For non-forward rules, only the interface meaningful for that direction is accepted; forward rules may carry both ingress and egress interfaces. AddRule performs one additional host-state check inside the daemon: any referenced interface must exist in the daemon's current network-interface inventory before UFW can be started. DeleteRule does not apply that existence check, because an interface may disappear while a rule that references it still needs to remain deletable.

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

### Network interface inventory

Network-interface discovery is an unsigned read path whose authority remains with the daemon, while presentation metadata belongs to `Ufw.Web`:

1. `Ufw.Systemd` enumerates the host's current network interfaces through the platform networking API and exposes their names through `GET /api/v1/network-interfaces` over IPC. The daemon deliberately does not filter by operational state, assigned IP addresses, or interface type: those properties are transient and interfaces such as disconnected wireless devices, VLANs, tunnels, or bridges can still be valid firewall targets. The daemon does not persist presentation metadata.
2. `Ufw.Web` stores a reconciled cache in PostgreSQL. Each cached interface has an internal numeric primary key, a stable UUIDv7 exposed to the frontend, the authoritative interface name, an optional ASP-owned comment, and an ASP-owned visibility flag controlling whether it is offered as a rule-editor suggestion. A separate cache-state row records the last successful reconciliation time, including the valid empty-inventory case.
3. Browser `GET /api/v1/network-interfaces` reads only the ASP cache. `POST /api/v1/network-interfaces/reconcile` explicitly refreshes it from the daemon: surviving names keep their UUID/comment/visibility metadata, new names receive a new UUIDv7 and are visible by default, and disappeared names are deleted together with their presentation metadata.
4. Comment and visibility updates are addressed by frontend UUID and never cross the IPC or signed-intent boundary. The rule editor only offers visible cached interfaces and can search them by either interface-name or comment substring, but the selected rule value is always the real interface name. Free-text entry remains available, including for interfaces hidden from suggestions.

The cache is therefore advisory for authoring and presentation only. Hiding an ASP cache entry does not make the underlying host interface invalid, and a stale visible entry cannot authorize use of an interface that no longer exists because AddRule independently checks the signed interface name against the daemon's current host inventory immediately before UFW execution.

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

UFW and `Ufw.Systemd` are authoritative for firewall rule existence and semantics. `Ufw.Web` owns application metadata and caches that do not change firewall meaning. The network-interface inventory is the first such cache: its comments and frontend UUIDs are ASP-owned presentation state, while interface existence is re-established from the daemon and independently validated again for AddRule. Future metadata such as authorship, semantic analysis, or reachability analysis follows the same boundary and cannot establish that a firewall rule exists or authorize a mutation.

Out-of-band UFW and host-network changes are expected. Rule listing observes firewall changes directly, semantic identity allows manually created supported rules to be addressed, and DeleteRule resolves against fresh daemon-observed state. Interface reconciliation explicitly observes host-network changes. If web metadata and daemon/host state disagree, reconciliation starts from the authoritative daemon state.

## Extension boundaries

New HTTP controllers belong under versioned API namespaces such as `Api/V1/Controllers`, with public request/response models under the corresponding API model area. Application services should remain independent of controller transport concerns.

New daemon operations use the typed IPC request/response and source-generated routing infrastructure. Read-only operations may remain unsigned when their data sensitivity permits. Mutating operations must reuse the signed-intent authorization boundary rather than treating IPC peer identity or ASP authorization as mutation authority.

New rule syntax is safe to expose for mutation only when parsing, normalization, validation, semantic identity, signed canonicalization, and UFW argument construction agree on its meaning. Unsupported UFW output should remain visible but unaddressable until that complete semantic path exists.
