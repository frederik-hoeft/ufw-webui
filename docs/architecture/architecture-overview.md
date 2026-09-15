# UFWeb Architecture

UFWeb is split around one central boundary: network-facing application code does not execute privileged firewall commands. The browser and web application provide management workflows, while a small host daemon owns UFW integration and independently authorizes every privileged mutation.

This document explains that system model, how state is owned, and how the major components interact. Protocol encoding details live under [Protocols](../protocols/README.md), and host-specific operational steps live under [Deployment](../deployment/deployment.md).

## System model

A production deployment has five application/runtime components around UFW itself: the administrator's browser, nginx, `Ufw.Web`, PostgreSQL, and the privileged `Ufw.Systemd` host daemon. Only the daemon crosses the host firewall boundary.

```mermaid
flowchart LR
    Browser[Browser\nUfw.Client]

    subgraph Containers[Application containers]
        Nginx[nginx\nstatic frontend + TLS]
        Web[Ufw.Web\nREST + auth + application state]
        Db[(PostgreSQL)]
        Nginx -->|Unix socket\n/api/* only| Web
        Web -->|private DB network| Db
    end

    Daemon[Ufw.Systemd\nprivileged host daemon]
    Ufw[UFW\nhost firewall state]

    Browser -->|HTTPS| Nginx
    Web -->|local IPC| Daemon
    Daemon -->|validated argv| Ufw
```

The public HTTP endpoint is nginx. It serves the independently built WebAssembly application and proxies only `/api/*` to `Ufw.Web`. `Ufw.Web` has no public TCP listener in the production Compose topology. `Ufw.Systemd` is not containerized: it runs under systemd so the application stack never needs access to UFW files, netfilter privileges, or the host Docker socket.

The split between nginx and `Ufw.Web` is security-significant. The browser creates privileged mutation signatures, so the code delivered to the browser is part of the signing trusted computing base. Keeping frontend assets in a separate, read-only nginx image prevents a compromised ASP process from replacing the signing application directly.

## Responsibility boundaries

### Browser application

`Ufw.Client` presents firewall state and application metadata, manages the browser side of authentication, validates rule input for usability, and creates signed mutation intents. It talks only to the versioned REST API; it has no knowledge of daemon transports or UFW process execution.

Access JWTs stay in memory. The refresh token is an `HttpOnly` cookie managed by the browser, and mutation private keys are supplied to the signing workflow without being persisted by the application. Appearance and culture preferences are the only browser-local persisted state.

Browser-side rule validation is not an authorization boundary. The daemon repeats semantic validation and reconstructs the canonical signed representation before it accepts a mutation.

### Web application

`Ufw.Web` owns HTTP concerns: API authentication, user/session state, application metadata, PostgreSQL persistence, and adaptation between REST and the local daemon protocol. It can ask the daemon to list state or submit a signed mutation, but it cannot manufacture mutation authority.

The web application intentionally does not maintain a second firewall model in PostgreSQL. Its database contains ASP.NET Core Identity data, refresh-token families, and application-owned authoring metadata such as network-interface comments and known-host aliases.

Database-backed workflows preserve one transactional boundary across related Identity and application state. Authentication therefore does not commit a refreshed token while rolling back the corresponding Identity state, or vice versa. Expected failures may still commit security-relevant state such as failed-login counters or refresh-family revocation.

### Privileged daemon

`Ufw.Systemd` is the host security boundary. It owns daemon IPC routing, authorized mutation keys, replay protection, deployment identity, authoritative UFW observation, host-interface validation, and the subprocess boundary.

The daemon exposes read operations for process liveness, rules, network interfaces, and intent context. Add, ordered insertion, delete, and reorder operations additionally cross signed-intent verification before they can reach the UFW execution gate. IPC peer identity and web authentication are defense in depth, not substitutes for this authorization check.

All UFW activity is serialized inside the daemon. A mutation retains the execution gate through current-state checks, process completion, and post-operation reconciliation. Once a child process starts, cancellation does not abandon it: the daemon retains ownership until the child exits or is terminated and reaped, reconciles authoritative state, and only then completes the request.

### Shared contracts

`Ufw.Shared` contains concepts that must mean the same thing on both sides of a process boundary: firewall rule semantics, normalization and rendering, signed-intent primitives, IPC message contracts, and protocol serialization metadata. It does not own runtime policy for either the browser, web application, or daemon.

`Ufw.Ipc.Client` implements the typed daemon client used by `Ufw.Web`. `Ufw.Roslyn` provides the runtime-facing routing and serialization abstractions, while `Ufw.Roslyn.SourceGen` resolves their compile-time contracts and emits static bindings suitable for NativeAOT. Controller routing and JSON serialization use independent contract families so their compile-time dependencies can evolve separately. See [Compile-time routing and serialization](source-generation.md) for the source-generation boundary.

## Operational status boundaries

Operational status is reported from explicit probes rather than inferred from unrelated successful endpoints. The browser treats the major runtime boundaries independently:

- `GET /api/health` probes the `Ufw.Web` management process. ASP also retains `/health` for direct/internal health checks; production nginx intentionally exposes only the `/api/*` form to the browser.
- `GET /api/v1/status` crosses ASP and local IPC to a dependency-free `Ufw.Systemd` endpoint. It proves that the daemon request path can answer, but it does not execute UFW or read deployment identity/replay state.
- `GET /api/v1/rules` remains the authoritative firewall/UFW probe. A daemon can therefore be live even when UFW observation fails.
- `GET /api/v1/intent/context` supplies deployment identity and signed-intent protocol metadata only. Its success or failure does not define daemon liveness.

Probe results do not rescue or overwrite each other. A successful rules read cannot make a failed daemon liveness probe healthy. Conversely, if an upstream probe fails while a downstream request also fails, the downstream component is reported as unknown rather than inferred unavailable; independently successful downstream probes remain visible.

## State ownership

The architecture distinguishes authoritative state from caches and presentation metadata. That distinction is what allows UFW to remain editable outside this application.

| State | Owner | Notes |
| --- | --- | --- |
| Firewall rules, enabled state, IPv6 capability, and default policies | UFW, observed through `Ufw.Systemd` | Never reconstructed from PostgreSQL |
| Current host network interfaces | Host OS, observed through `Ufw.Systemd` | Re-read for append and ordered-insertion validation |
| Authorized mutation public keys | `Ufw.Systemd` operator state | Not writable through the web API |
| Signed-intent replay records and deployment identity | `Ufw.Systemd` | Persisted across daemon restarts |
| Active reorder recovery journal | `Ufw.Systemd` | Durable safety record while a delete/reinsert move may be incomplete |
| Users, refresh-token families, interface metadata, known-host aliases | `Ufw.Web` / PostgreSQL | Application state only |
| Access token | Browser memory | Short-lived bearer credential |
| Mutation private key | Administrator/browser signing workflow | Never sent to the server or persisted by the application |
| Production frontend assets | nginx image | Built/deployed independently from ASP |

Daemon-derived interface metadata is reconciled against host state, and host state wins when they disagree. Known-host aliases are independently ASP-owned authoring metadata and have no daemon inventory to reconcile. Neither kind of metadata can create firewall authority: it must resolve to literal firewall semantics before signing.

## Primary request flows

### Reading firewall state

The browser calls the authenticated REST API, `Ufw.Web` sends a typed local IPC request, and the daemon reads `ufw status numbered` while holding the UFW execution gate. Supported rows are parsed into the shared semantic rule model and receive stable semantic identities. Rows the parser cannot understand completely remain visible as raw state but do not receive a mutable identity. The same read also loads UFW's host configuration from the configured defaults file, so one rule snapshot carries the effective IPv6 capability and incoming, outgoing, and routed default policies alongside the rule list.

The browser treats each successful response as an authoritative snapshot. UFW keeps IPv4 and IPv6 in independent ordered rule sets and concatenates them for numbered status output, so the browser presents separate family sections while retaining the exact combined snapshot coordinates for signing and mutation addressing. It displays the default policies with the rules and uses the daemon-reported IPv6 capability to constrain IPv6 authoring rather than inferring support locally. If a later refresh fails, the previous snapshot may remain visible as stale information, but mutation controls are disabled until a fresh authoritative read succeeds.

### Mutating firewall state

A firewall mutation uses two independent authorization layers. The HTTP request requires a valid web session, and the mutation body carries a browser-created signature that the daemon verifies independently.

The browser first obtains the daemon's intent context and signs an operation-specific canonical payload with an authorized P-256 key. Append add binds normalized rule semantics; delete also binds the semantic rule identity. Ordered insertion binds the normalized new rule, a SHA-256 fingerprint of the exact ordered firewall-list projection displayed by the browser, one snapshot-local anchor occurrence, and before/after placement. Reorder binds the same ordered-list fingerprint plus the complete desired occurrence permutation. Operational UFW configuration carried with the rule response is checked independently rather than being folded into fingerprint version 1. `Ufw.Web` forwards the signed envelope without becoming mutation authority. The daemon verifies deployment scope, operation, payload semantics, signature, and freshness before entering the serialized mutation boundary, then durably consumes the nonce and reconciles every privileged UFW effect against fresh authoritative state.

See [Firewall model](firewall-model.md) for state reconciliation and [Signed mutation intent v2](../protocols/signed-intent.md) for the exact signed contract.

### Managing network-interface metadata

The daemon exposes the host's current interface names as an unsigned read operation at the mutation-protocol layer. `Ufw.Web` can explicitly reconcile that host inventory into PostgreSQL, preserving application-owned comments and visibility flags for names that still exist.

The cached inventory is an authoring aid, not firewall authority. Selecting an interface in the UI writes the real interface name into the rule. Immediately before an add or ordered-insertion operation executes, the daemon independently verifies that every referenced interface still exists on the host. Deletion remains possible after an interface disappears so stale firewall rules do not become undeletable.

### Managing known hosts for rule authoring

`Ufw.Web` owns a separate PostgreSQL catalog of known-host aliases. Each entry has an application identity, a human-facing name and optional comment, a visibility preference, and one canonical literal IPv4/IPv6 host address or CIDR. Unlike network-interface metadata, these entries are not derived from daemon or operating-system inventory and require no daemon reconciliation.

The browser uses visible aliases only as autocomplete suggestions while preserving unrestricted literal address entry. Selecting an alias immediately writes its canonical address into the source or destination field of `FirewallRuleSpecification`; the alias ID, name, comment, and visibility flag do not enter rule rendering, signed intents, REST mutation payloads, IPC, or daemon processing. Changing or deleting an alias therefore cannot change a rule that was already authored.

An alias keeps its address family for its lifetime. Same-family address changes are allowed, but changing an existing IPv4 alias into IPv6 or vice versa is rejected so one persistent alias identity cannot silently change network-family meaning. Hiding an alias affects suggestions only and has no effect on firewall validity.

### Authenticating the web session

Users authenticate against ASP.NET Core Identity. Successful login returns a short-lived ES256 access token and sets a rotating opaque refresh token in a `Secure`, `HttpOnly`, `SameSite=Strict` cookie. Only refresh-token hashes are persisted.

Refresh tokens belong to families and rotate on use. Replay of a revoked token invalidates remaining active members of the family. The browser serializes login, refresh, and logout operations across same-origin tabs so two tabs do not race to consume the same rotating cookie.

Web authentication controls REST access. It is deliberately separate from daemon mutation authorization.

## IPC boundary

`Ufw.Web` and `Ufw.Systemd` communicate over a connection-oriented local stream. Linux production uses a group-restricted Unix-domain socket; development can use the corresponding local named-pipe abstraction. Optional TLS or mTLS can wrap that stream.

The protocol is layered so each level owns one kind of compatibility decision:

1. the stream establishes ordered byte transport and optional TLS;
2. ITP frames and bounds one application message;
3. the application protocol validates request/response envelope semantics;
4. daemon routing binds a valid request to a typed endpoint.

Each connection carries one request/response exchange. There is no long-lived IPC session, multiplexing, or request correlation state. Expected peer and protocol failures are contained to the current connection; unexpected daemon failures remain visible rather than being silently converted into peer errors.

See [IPC protocols](../protocols/README.md) for the wire contracts.

## Project structure

The source tree follows deployment and responsibility boundaries rather than mirroring individual screens or endpoints.

| Project | Architectural role |
| --- | --- |
| `Ufw.Client` | browser application and REST client |
| `Ufw.Web` | web/API application and PostgreSQL-backed application state |
| `Ufw.Systemd` | privileged firewall daemon |
| `Ufw.Shared` | cross-process domain/protocol contracts |
| `Ufw.Ipc.Client` | local typed IPC client |
| `Ufw.Roslyn` / `Ufw.Roslyn.SourceGen` | runtime abstractions plus compile-time routing and serialization generation |
| `Ufw.Mock` | development substitute for the external UFW executable |

Tests are split along the same boundaries. Shared tests cover firewall/protocol semantics, IPC tests exercise the real client/daemon protocol stack over in-process transport, daemon tests cover authorization and UFW integration behavior, web tests cover persistence and application workflows, and mock black-box tests verify observable CLI compatibility.

## Architectural invariants

The system model depends on five invariants:

- firewall existence and semantics are authoritative in UFW rather than PostgreSQL;
- any mutation that can affect UFW crosses daemon-side authorization, independently of HTTP authentication;
- application metadata can enrich authoring or presentation only after resolving to real firewall semantics before signing;
- unsupported UFW syntax remains visible and read-only until parsing, normalization, identity, signing, and argv rendering agree on its meaning;
- production frontend delivery remains independent from ASP while browser code handles privileged signing material.

Internal classes and service boundaries can evolve without changing these ownership and security properties.
