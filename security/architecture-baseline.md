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

Browser-side key storage and UX remain outside the current implementation. The signed-intent protocol is specified and verified on the daemon: ECDSA P-256, SHA-256, IEEE P1363 signatures, field-oriented canonicalization, and `sha256:` key IDs derived from SubjectPublicKeyInfo. A first client prototype may prompt for the private key on each signature.

### Ufw.Web

`Ufw.Web` is network-facing and should be treated as compromiseable relative to the privileged daemon. It authenticates users, applies application-level authorization, provides richer data to the frontend, and proxies allowed requests to local IPC.

Its JWT and refresh-token system protects the HTTP API session boundary. It does not elevate the web process into a firewall mutation authority.

A compromise of `Ufw.Web` may expose web-session material, application metadata, and data observable by the web process. It may also suppress requests or misrepresent daemon-observed state to the browser. The security goal addressed by the privilege boundary is narrower: such compromise still cannot forge a new user-approved firewall mutation.

### Local IPC boundary

The daemon accepts requests only through the configured local named-pipe/Unix-domain endpoint. The active code path does not provide a TCP transport.

Unix socket ownership and permissions should restrict which local principals can connect. This reduces attack surface but does not establish user intent: `Ufw.Web` is expected to be able to connect, and therefore a compromised `Ufw.Web` can also connect.

### Ufw.Systemd

`Ufw.Systemd` is the privileged security boundary and the authority for UFW state. It must independently validate any request capable of changing firewall state before executing it.

The daemon independently verifies every mutating request before executing UFW. Compromise of `Ufw.Web` is not sufficient to produce an accepted mutation.

## Signed mutation invariant

Before a mutating IPC operation is added, the protocol must ensure that the daemon can verify that the user authorized the exact mutation being requested.

At minimum, the signed material must bind the user's authorization to the complete mutation intent rather than to an ASP-generated interpretation of it. The signed representation must also have explicit protocol/domain scope so a signature cannot be reinterpreted as a different operation or replayed in an unintended deployment context. The daemon must validate the signature against an authorized public key that is available to the daemon through a trust path that a compromised `Ufw.Web` cannot unilaterally rewrite.

The mutation protocol prevents an intercepted valid signed intent from being replayed as a fresh authorization. Replay protection uses a unique nonce persisted on disk so it survives daemon restarts, combined with an issued-at window (`max_intent_age` plus `clock_skew`). Nonce consumption is serialized with UFW execution. The signed material is a field-oriented canonical encoding of version, key id, timestamp, nonce, operation (`rules.add` / `rules.delete`), and the normalized rule fields. JSON key order and whitespace are not part of the signed encoding.

`Ufw.Web` may perform ordinary application authorization before forwarding a request, but the daemon's signature verification remains mandatory. ASP authorization can reduce what a legitimate session is offered; it cannot substitute for daemon verification.

## Authorized public keys

The daemon needs access to the public keys that are permitted to authorize mutations. That authorization set cannot be sourced solely from a database controlled by `Ufw.Web`, because doing so would allow a compromised web process to register an attacker key and then sign arbitrary mutations.

Authorized public keys are loaded from a daemon-local PEM file (`security.authorized_keys_path`), analogous to `authorized_keys`. The file is operator-managed. A compromised `Ufw.Web` cannot register a new trusted key. Only ECDSA P-256 keys are accepted.

## Firewall state authority

The daemon and UFW are authoritative for the actual firewall configuration. `Ufw.Web` may store metadata associated with a stable daemon-visible rule identifier, for example an identifier encoded in a UFW comment if that proves operationally viable.

Web metadata may describe authorship, presentation, or other application concerns, but it cannot override daemon-observed rule state. If web metadata and UFW disagree, reconciliation starts from the daemon/UFW state.

## Web authentication state

The HTTP API uses ASP.NET Core Identity and short-lived RSA-signed JWT access tokens. Refresh tokens are opaque random values stored in a `Secure`, `HttpOnly`, `SameSite=Strict` host-prefixed browser cookie; only hashes are stored in SQLite.

Refresh tokens rotate on every successful refresh. Reuse of an already-revoked token invalidates the active token family. The user's Identity security stamp is captured with the token family so password/security-state changes prevent continued refresh from stale families. Account confirmation and lockout checks are enforced before issuing refreshed access tokens.

Access JWTs remain valid until their short expiration even after a refresh family is revoked. This is an intentional property of stateless access tokens and should be accounted for when choosing the access-token lifetime.

## Current limitations

This baseline describes the security boundary that the current preparatory state preserves and the invariant required before write support is introduced.

The following remain out of scope:

- browser/Blazor UI and in-browser key storage
- dynamic authorized-key lifecycle / user provisioning
- ASP-side firewall state reconciliation and audit logging
- EditRule and other future mutation types (the intent verifier is reusable for them)
- mandatory mTLS; TLS/mTLS is configurable defense-in-depth and is skipped when `SslProtocols` is `None`

Delete requests carry the content-addressed rule id plus the full specification. The daemon recomputes the identity, re-lists UFW under the execution lock, and refuses the delete if the rule is missing or no longer unique. `ufw status numbered` indexes are never accepted from the client.
