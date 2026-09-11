# Security Architecture

UFW WebUI is designed around the assumption that the network-facing web tier is a larger and less trusted attack surface than the privileged firewall daemon. The architecture therefore separates ordinary web authentication from authorization to execute a firewall mutation.

The security objective is not to make `Ufw.Web` harmless if compromised. It is to prevent compromise of the ASP process alone from becoming sufficient authority to forge a new valid firewall mutation or silently replace the browser signing client.

## Trust boundaries

The major trust relationships are:

- the browser trusts the frontend artifact it receives to construct and display the mutation being signed;
- `Ufw.Web` authenticates users and controls access to the REST API, but is not trusted to authorize privileged firewall changes by itself;
- `Ufw.Systemd` independently verifies mutation signatures against operator-managed public keys and is the only component allowed to execute UFW;
- UFW and the host operating system remain authoritative for firewall and interface state;
- PostgreSQL is trusted for application/authentication persistence, not for firewall truth.

A production deployment also isolates the frontend artifact from ASP. nginx serves an independently built, read-only client image and proxies only `/api/*` to `Ufw.Web` over a private Unix socket. ASP has neither the frontend files nor a public listener. This prevents an ASP compromise from directly rewriting the browser application to capture an administrator's signing key.

Frontend delivery is also intentionally strict about executable browser content. nginx applies a restrictive Content Security Policy whose inline-script allowance is derived from the exact generated import map in the built client artifact; missing framework/application assets fail as real static-file errors instead of falling through to the SPA shell. This keeps frontend integrity failures visible rather than weakening script policy to accommodate them.

## Two authorization planes

Web authentication and firewall mutation authorization answer different questions.

A JWT access token answers whether the HTTP caller may use an API endpoint. It is issued by `Ufw.Web` after ASP.NET Core Identity authentication and is validated by the REST layer.

A signed intent answers whether an authorized administrator approved one exact privileged firewall mutation for one daemon deployment. It is created in the browser and verified by `Ufw.Systemd`. `Ufw.Web` forwards the signed envelope but cannot replace it with an ASP-generated authorization decision.

Both checks normally apply to a browser mutation. Passing only one is insufficient.

## Signed mutation authorization

Add and delete use the versioned signed-intent protocol defined in [Signed mutation intent v2](../protocols/signed-intent.md). The signed bytes bind:

- the intent protocol version and domain;
- daemon deployment identity;
- authorized-key identifier;
- issuance time;
- random nonce;
- operation name;
- the complete normalized rule semantics;
- the semantic rule identity for delete.

JSON formatting is not an authorization input. The daemon validates and normalizes the semantic payload, reconstructs the canonical byte representation, and verifies an ECDSA P-256/SHA-256 signature against its local authorized-key store.

The deployment identifier prevents a request signed for one daemon installation from being replayed against another. The operation field prevents cross-operation substitution. Binding the normalized rule prevents ASP or any intermediary from changing the material firewall semantics without invalidating the signature.

## Replay and freshness

A valid signature is intentionally single-use. Every intent carries an issuance timestamp and random nonce.

The daemon checks clock skew and maximum intent age before entering the privileged mutation boundary. Once verification succeeds, it serializes the mutation under the UFW execution gate and durably consumes the nonce before invoking UFW. Replay records survive daemon restart and remain present through the same expiry boundary used by signature validation.

Corrupt, unreadable, or unwritable replay state fails closed. Concurrent copies of one valid intent cannot both pass nonce consumption, and restarting the daemon does not make a still-valid request reusable.

Authorized keys, deployment identity, and replay state are daemon/operator-owned files. The web API exposes no key-enrollment or replay-state mutation endpoint.

## Semantic target integrity

UFW row numbers are presentation state, not mutation authority. They change when rules are inserted or removed and cannot safely identify a previously viewed rule.

Delete therefore signs a normalized concrete rule plus its semantic identity. The daemon verifies that the identity matches the rule, re-lists UFW while holding its execution gate, and requires exactly one current match. Only then does it use that match's current number for the UFW subprocess.

Rules that cannot be parsed and semantically validated completely remain visible but have no mutable identity. This prevents partial parser understanding from becoming deletion authority.

See [Firewall state and rule model](firewall-model.md) for the complete identity and reconciliation model.

## Privileged process boundary

Signed authorization grants permission only for the supported semantic operation. The daemon validates the structural rule and renders argv directly; user-controlled rule text is never interpolated into a shell command.

For add, referenced interfaces must also exist in the daemon's current host-interface snapshot. This check is independent of ASP's cached interface metadata. Delete omits the existence check so stale rules remain removable after an interface disappears.

The daemon serializes UFW activity and keeps ownership of a started child until it exits or has been terminated and reaped. It verifies the authoritative UFW postcondition before returning success. Process cancellation, an ambiguous listing, or a successful exit code without the expected state transition cannot be promoted into a confirmed mutation.

## IPC transport security

The local IPC endpoint is access-controlled by operating-system permissions. Production uses a group-restricted Unix-domain socket exposed only to the ASP container.

TLS is optional and independent of signed-intent authorization. When enabled, the IPC client authenticates the server certificate. The daemon may additionally require and validate a client certificate, producing mTLS. Disabling TLS leaves confidentiality and peer admission to the local socket boundary, but it does not weaken the separate requirement for a valid signed mutation.

The IPC protocol itself is versioned and bounded so malformed framing or application messages are rejected before controller execution. See [IPC protocols](../protocols/README.md).

## Web authentication and session state

`Ufw.Web` uses ASP.NET Core Identity with short-lived ES256 access tokens. Refresh tokens are random opaque values delivered in a `Secure`, `HttpOnly`, `SameSite=Strict` host-prefixed cookie. Only token hashes are stored in PostgreSQL.

A successful refresh rotates the token. Reuse of a revoked token invalidates remaining active members of its token family. Families also capture the user's Identity security stamp so later account-security changes prevent indefinite refresh from stale credentials. Lockout and account-confirmation policy is checked before new access tokens are issued.

Because the refresh cookie is shared by tabs, the browser serializes login, refresh, and logout through a same-origin exclusive lock. The production topology consequently uses one consistent HTTPS browser origin for tabs sharing that cookie.

Revoking a refresh family does not revoke an already-issued JWT immediately. Access tokens remain valid until their short expiration; deployment policy should choose the access-token lifetime accordingly.

## Browser signing keys

The UI accepts an unencrypted PKCS#8 P-256 private key when a mutation is signed. The application clears that input after the operation and does not write it to local storage, PostgreSQL, ASP configuration, or daemon state. Only the corresponding public key belongs in the daemon's `authorized_keys` file.

This is intentionally a minimal key-handling model. Persistent browser key enrollment, WebAuthn/hardware-backed keys, revocation workflows, and user-to-key administration require their own design rather than silently expanding the current storage model.

## Security limits

The architecture does not claim to solve:

- compromise of the administrator browser or extensions;
- compromise of the independently served frontend/nginx image itself;
- dynamic authorized-key enrollment or user-to-key lifecycle management;
- immediate revocation of already-issued access JWTs;
- security audit/accountability logging;
- cross-process locking against an administrator or unrelated program invoking UFW concurrently;
- privileged mutation types beyond those with an explicitly defined signed-intent contract.

Out-of-band UFW changes are supported at the state-model level, but a truly simultaneous external UFW mutation can still race a daemon operation. The daemon fails conservatively when it cannot establish a unique authoritative result.
