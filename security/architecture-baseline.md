# Security Architecture Baseline

## Security objective

The architecture separates internet-facing account/session handling from privileged firewall execution. The central security requirement is stronger than process separation: compromise of `Ufw.Web` must not, by itself, give an attacker the ability to create an accepted firewall mutation.

The privileged daemon therefore does not treat any of the following as sufficient mutation authorization:

- the fact that a request arrived over the local IPC endpoint;
- possession of the web application's JWT signing key;
- a valid browser-to-ASP JWT;
- an ASP.NET Core Identity role or authorization decision asserted only by `Ufw.Web`;
- data stored only in the web application's database;
- successful TLS or mTLS authentication of the IPC peer.

These mechanisms reduce exposure and scope the HTTP/API surface, but the daemon's proof of user authorization is the signed mutation intent.

## Components and trust boundaries

### Browser or API client

The client is the user-authorization boundary for firewall mutations. `Ufw.Client` creates signed intents in the browser. Its first signing UX accepts an unencrypted PKCS#8 P-256 private key for one mutation at a time, uses Web Crypto for signing, and clears the request-local input afterward. The private key is not persisted in browser storage.

The client obtains the daemon's current intent context before signing. The context supplies the signed-intent protocol version and stable daemon deployment identifier. The private signing key remains client-side; only corresponding public keys are configured as trusted mutation authorities on the daemon.

The browser reuses the shared rule validator to provide immediate field-level feedback before signing. That validation is a usability aid, not a trust decision: `Ufw.Systemd` independently validates the signed rule specification before accepting the intent.

### Ufw.Web

`Ufw.Web` is network-facing and should be treated as compromiseable relative to the privileged daemon. It authenticates users, applies application-level authorization, and proxies daemon-backed requests over local IPC.

Its JWT and refresh-token system protects the HTTP session boundary. It does not make the web process a firewall mutation authority. A compromised `Ufw.Web` can observe, suppress, replay, alter, or manufacture IPC requests, but it cannot produce a new accepted mutation without a valid signature from a daemon-authorized key.

The web process may eventually store metadata or computed analysis around firewall rules. Such state cannot override UFW state and cannot modify the daemon's authorized-key set, deployment identity, or replay state.

### Local IPC boundary

The daemon accepts requests through the configured local named-pipe/Unix-domain endpoint; it does not expose a TCP listener. Filesystem/pipe permissions restrict which local principals can connect and therefore reduce local attack surface.

TLS can optionally wrap this stream. Server authentication is required whenever TLS is enabled, and optional client-certificate validation provides mTLS. These controls authenticate and protect the transport peer, but `Ufw.Web` is an expected peer and is explicitly included in the compromise model. Transport authentication therefore cannot substitute for end-user intent authorization.

### Ufw.Systemd

`Ufw.Systemd` is the privileged security boundary and the authority for UFW state. Every request capable of changing firewall state must cross the daemon's signed-intent verification boundary before a privileged UFW operation can start.

The daemon also owns the state required to enforce that boundary: authorized public keys, deployment identity, and replay records. Those files are operator/daemon-managed rather than writable through the Web API.

## Signed mutation authorization

AddRule and DeleteRule use a common versioned signed-intent protocol. The signed bytes bind:

- the protocol/domain identifier;
- daemon deployment identity;
- authorized-key identifier;
- issued-at time;
- unique nonce;
- operation (`rules.add` or `rules.delete`);
- the complete normalized rule semantics;
- the semantic `ruleId` for DeleteRule.

This binding prevents a valid signature from being moved to another daemon deployment, mutation type, or materially different rule. JSON property ordering and whitespace are not security inputs; signatures cover the field-oriented canonical representation defined by [the signed-intent protocol](../docs/protocols/signed-intent.md).

The daemon validates the payload semantics and normalizes the rule before reconstructing the canonical signed bytes. DeleteRule additionally requires a concrete family-specific rule specification and verifies that its supplied `ruleId` equals the daemon-computed semantic identity.

Signatures use ECDSA P-256 with SHA-256 and IEEE P1363 signature encoding. Key identifiers are SHA-256 fingerprints of SubjectPublicKeyInfo. The authorized-key file accepts public-key PEM blocks only.

## Freshness and replay protection

A valid signature is not reusable authorization. Every intent carries a random nonce and issued-at timestamp. The daemon accepts issued-at values within the configured clock skew and treats intent validity as a half-open interval ending at `issuedAt + max_intent_age + clock_skew`.

Nonce consumption occurs under the same in-process execution gate that serializes UFW access. Before a mutation process can start, the nonce is persisted to daemon-owned replay state and flushed durably. The replay record is retained through the exact same expiry boundary used by signature validation, so there is no interval in which an intent remains valid after its replay record may be discarded.

Replay state is loaded after daemon restart. Malformed replay state or persistence failure fails closed rather than allowing the mutation to proceed without durable replay protection. Concurrent copies of the same signed request therefore cannot both cross the mutation boundary, and restarting the daemon does not make a still-valid intent reusable.

## Deployment scope and authorized keys

The daemon persists a random deployment identifier in `security.deployment_id_path`. That identifier is exposed through the unsigned intent-context read endpoint and is part of every signed mutation. Copying a valid signed request to another daemon deployment therefore invalidates the signature or deployment check.

Authorized public keys are loaded from `security.authorized_keys_path`. The file is manually/operator managed, may contain comments and P-256 `PUBLIC KEY` PEM blocks, and is not writable through the Web API. Missing authorized-key configuration leaves mutation authorization empty; malformed or unsupported key material fails closed.

Dynamic key enrollment, user-to-key lifecycle management, and revocation APIs are separate concerns from the current mutation protocol.

## Semantic rule identity and out-of-band changes

UFW is the sole authority for firewall configuration. Administrators and other tools may change it outside UFW WebUI, so neither ASP state nor stale `ufw status numbered` positions can safely identify a rule.

Supported listed rules receive a content-derived semantic identity based on normalized action, address family, direction, protocol, addresses, ports, and interfaces. Comments and numbered-list positions are excluded. IPv4 and IPv6 rows have distinct identities. Semantically equivalent textual representations normalize to the same identity.

DeleteRule signs both the concrete semantic specification and its `ruleId`. The daemon re-lists UFW under the execution gate and resolves that identity to the current numbered row immediately before deletion. A missing or non-unique identity is rejected. Numbered positions supplied by a client are never accepted as mutation targets.

Rows that cannot be fully parsed and semantically validated remain visible to read clients but receive no mutable identity. This prevents partial parser understanding from becoming authority to modify an unsupported rule.

## Privileged process boundary

Signed authorization grants permission only for the supported semantic UFW operation. Mutation input is validated and converted into argv controlled by the daemon; it is never interpolated into a shell command.

All UFW process activity is serialized in-process. The daemon owns a started child until it exits or has been terminated and reaped, even when the originating request is canceled. The execution gate remains held while interrupted mutations are reconciled against authoritative UFW state.

UFW runs under a deterministic locale. Parseable stdout and diagnostic stderr are kept separate. A zero exit code alone is not considered confirmed mutation success: the daemon re-reads UFW and returns a normal success response only when the expected semantic postcondition can be observed. If execution or reconciliation cannot establish a safe result, the daemon reports failure/uncertainty rather than manufacturing a confirmed state.

## Optional TLS and mTLS

Transport encryption is defense in depth and is configured independently from signed-intent authorization.

TLS enablement is explicit. When enabled, the IPC client validates the server certificate against the configured server identity. `SslProtocols.None` keeps its standard .NET behavior and delegates protocol selection to the runtime/OS; administrators may explicitly constrain the allowed protocol set when policy requires it.

The daemon can optionally require a client certificate by configuring remote-certificate validation. In that mode the web IPC client presents its configured certificate and the daemon validates its trust chain and configured subject/issuer constraints. Without client-certificate validation, TLS still authenticates the server but does not require mTLS.

Disabling TLS leaves the local stream unencrypted and relies on local endpoint permissions for transport exposure. Regardless of transport mode, a mutating request still requires a valid signed intent.

## Web authentication state

The HTTP API uses ASP.NET Core Identity and short-lived RSA-signed JWT access tokens. Refresh tokens are opaque random values stored in a `Secure`, `HttpOnly`, `SameSite=Strict` host-prefixed browser cookie; only hashes are stored in SQLite.

Refresh tokens rotate on every successful refresh. Reuse of an already-revoked token invalidates the active token family. The user's Identity security stamp is captured with the token family so password/security-state changes prevent continued refresh from stale families. Account confirmation and lockout checks are enforced before issuing refreshed access tokens.

Access JWTs remain valid until their short expiration even after a refresh family is revoked. This is an intentional property of stateless access tokens and should be accounted for when choosing the access-token lifetime.

## Out-of-scope security concerns

The current boundary intentionally does not implement:

- persistent, hardware-backed, or otherwise managed browser key storage and key-enrollment UX;
- dynamic authorized-key enrollment, revocation, or user/key lifecycle APIs;
- ASP-side firewall state reconciliation or firewall metadata authority;
- security audit logging/accountability infrastructure;
- EditRule or other mutation types beyond the reusable intent-verification primitives;
- cross-process locking against administrators or unrelated programs invoking UFW concurrently.

Out-of-band UFW changes remain supported at the semantic state level, but a simultaneous external UFW mutation can still create an unavoidable cross-process time-of-check/time-of-use race. Daemon-internal requests are serialized and do not have that race with each other.
