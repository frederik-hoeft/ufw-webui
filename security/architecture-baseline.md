# Security Architecture Baseline

## Security objective

The architecture separates internet-facing account/session handling from privileged firewall execution. The central security requirement is stronger than process separation: compromise of `Ufw.Web` must not, by itself, give an attacker the ability to create arbitrary accepted UFW mutations.

The privileged daemon therefore cannot treat any of the following as sufficient authorization for a firewall change:

- the fact that a request arrived over the local IPC socket
- possession of the web application's JWT signing key
- a valid browser-to-ASP JWT
- an ASP.NET Core Identity role or authorization decision asserted only by `Ufw.Web`
- data stored only in the web application's database

These mechanisms remain useful for scoping the HTTP/API surface, but none is the daemon's proof of end-user approval for privileged mutation.

## Components and trust boundaries

### Browser frontend

The browser is the user interaction boundary. A future Blazor frontend is expected to support cryptographic signing of firewall mutation intents in the browser. The frontend itself is not privileged and is not authoritative for host firewall state.

Browser-side signing is outside the current implementation. No assumption is made here about key storage technology or a specific signature algorithm.

### Ufw.Web

`Ufw.Web` is network-facing and should be treated as compromiseable relative to the privileged daemon. It authenticates users, applies application-level authorization, provides richer data to the frontend, and proxies allowed requests to local IPC.

Its JWT and refresh-token system protects the HTTP API session boundary. It does not elevate the web process into a firewall mutation authority.

A compromise of `Ufw.Web` may expose web-session material, application metadata, and data observable by the web process. It may also suppress requests or misrepresent daemon-observed state to the browser. The security goal addressed by the privilege boundary is narrower: such compromise still cannot forge a new user-approved firewall mutation.

### Local IPC boundary

The daemon accepts requests only through the configured local named-pipe/Unix-domain endpoint. The active code path does not provide a TCP transport.

Unix socket ownership and permissions should restrict which local principals can connect. This reduces attack surface but does not establish user intent: `Ufw.Web` is expected to be able to connect, and therefore a compromised `Ufw.Web` can also connect.

### Ufw.Systemd

`Ufw.Systemd` is the privileged security boundary and the authority for UFW state. It must independently validate any request capable of changing firewall state before executing it.

The current daemon contains no mutating controller endpoint. Mutation support remains blocked on the signed-intent verification design below.

## Signed mutation invariant

Before a mutating IPC operation is added, the protocol must ensure that the daemon can verify that the user authorized the exact mutation being requested.

At minimum, the signed material must bind the user's authorization to the complete mutation intent rather than to an ASP-generated interpretation of it. The signed representation must also have explicit protocol/domain scope so a signature cannot be reinterpreted as a different operation or replayed in an unintended deployment context. The daemon must validate the signature against an authorized public key that is available to the daemon through a trust path that a compromised `Ufw.Web` cannot unilaterally rewrite.

The mutation protocol must prevent an intercepted valid signed intent from being replayed as a fresh authorization. The precise canonicalization, deployment/daemon scope, freshness/replay mechanism, key lifecycle, and signature algorithm are intentionally not fixed by this baseline; they must be designed as one protocol so that verification semantics are unambiguous on both the browser and daemon sides.

`Ufw.Web` may perform ordinary application authorization before forwarding a request, but the daemon's signature verification remains mandatory. ASP authorization can reduce what a legitimate session is offered; it cannot substitute for daemon verification.

## Authorized public keys

The daemon needs access to the public keys that are permitted to authorize mutations. That authorization set cannot be sourced solely from a database controlled by `Ufw.Web`, because doing so would allow a compromised web process to register an attacker key and then sign arbitrary mutations.

The provisioning and lifecycle mechanism for authorized keys is not implemented in the current stage. Whatever mechanism is selected must preserve daemon-side control over the effective trust set.

## Firewall state authority

The daemon and UFW are authoritative for the actual firewall configuration. `Ufw.Web` may store metadata associated with a stable daemon-visible rule identifier, for example an identifier encoded in a UFW comment if that proves operationally viable.

Web metadata may describe authorship, presentation, or other application concerns, but it cannot override daemon-observed rule state. If web metadata and UFW disagree, reconciliation starts from the daemon/UFW state.

## Web authentication state

The HTTP API uses ASP.NET Core Identity and short-lived RSA-signed JWT access tokens. Refresh tokens are opaque random values stored in a `Secure`, `HttpOnly`, `SameSite=Strict` host-prefixed browser cookie; only hashes are stored in SQLite.

Refresh tokens rotate on every successful refresh. Reuse of an already-revoked token invalidates the active token family. The user's Identity security stamp is captured with the token family so password/security-state changes prevent continued refresh from stale families. Account confirmation and lockout checks are enforced before issuing refreshed access tokens.

Access JWTs remain valid until their short expiration even after a refresh family is revoked. This is an intentional property of stateless access tokens and should be accounted for when choosing the access-token lifetime.

## Current limitations

This baseline describes the security boundary that the current preparatory state preserves and the invariant required before write support is introduced.

The following are intentionally not implemented yet:

- browser/Blazor UI
- browser-side signing keys and signing UX
- canonical signed mutation format
- daemon-side signature verification
- daemon-managed authorized public-key lifecycle
- cryptographic peer authentication on the local IPC stream; local endpoint permissions scope connectivity but do not authorize mutations
- mutating HTTP controllers
- mutating daemon IPC endpoints
- stable UFW rule identifiers and metadata reconciliation

Read-only infrastructure can evolve independently, but no firewall mutation path should bypass these missing controls.
