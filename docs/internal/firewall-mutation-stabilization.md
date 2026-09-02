# Firewall mutation stabilization plan

## Purpose

The current mutation branch establishes the first end-to-end rule-management path: authenticated HTTP requests are proxied through typed IPC, `Ufw.Systemd` verifies user-signed mutation intents, and the daemon reads and mutates authoritative UFW state. The overall trust boundary is sound enough to use as the implementation baseline, but several protocol, rule-model, process-lifecycle, and transport details still need to be stabilized before the work is suitable for upstream integration.

This document is a temporary implementation plan. It records the current state, the intended steady-state contracts, and the order in which the remaining work should be completed. Once the implementation is stable, the durable architectural material belongs in `docs/architecture.md`, `security/architecture-baseline.md`, and the protocol documentation rather than in this planning document.

## Current state

### Mutation authorization

`AddRule` and `DeleteRule` use a shared signed-intent envelope. `Ufw.Web` authenticates the HTTP request but forwards the signed mutation without becoming the daemon's mutation authority. `Ufw.Systemd` verifies ECDSA P-256 signatures against an operator-managed authorized-key file and consumes nonces through a persistent file-backed replay store before executing UFW.

The design is directionally correct, but the signed domain does not yet bind an intent to a specific daemon deployment. A valid signed intent captured for one deployment can therefore be presented to another deployment that trusts the same signing key. Replay lifetime handling also has an exact-boundary mismatch: the verifier can still accept an intent at the timestamp at which the nonce store considers its replay record expired. Malformed payload decoding and replay-store corruption also need explicit fail-closed behavior.

### Rule state and identity

Rule listing parses supported `ufw status numbered` rows into a semantic rule specification and derives a content-addressed identifier from normalized match/action fields. Delete requests carry both the semantic identifier and the expected rule specification; the daemon re-reads UFW while holding its execution gate and resolves the current display number immediately before deletion. Numbered UFW rows are presentation data rather than stable client-facing identities.

The current semantic model is not yet faithful enough to be an identity boundary. In particular, non-forward rules accept both source and destination interfaces while command construction silently chooses one. Two distinct signed specifications can therefore collapse to the same UFW command. Address normalization is IPv4-only and does not canonicalize CIDRs to their network address, port normalization is textual rather than fully semantic, and the parser accepts a recognized prefix without requiring complete consumption of the input row. IPv6 rows are currently exposed only as unparsed read-model entries.

### UFW execution

All daemon-managed UFW operations share an in-process execution gate. Rule input is converted into explicit argument-vector entries and is not sent through a shell, which provides a good command-injection boundary. Add and Delete re-read authoritative UFW state for domain checks instead of maintaining a daemon-side rule database.

Cancellation after a child process has started is not yet coupled to process ownership. A canceled wait can release the execution gate while the UFW process continues running. Locale/output handling is also implicit, and successful Add currently permits a response whose expected rule cannot be confirmed in the post-mutation readback.

### Transport security

The IPC transport already has TLS and certificate-validation infrastructure. The current mutation baseline incorrectly treats `SslProtocols.None` as a request to disable TLS. In .NET, `SslProtocols.None` means that the runtime/OS selects the TLS protocol. TLS enablement therefore needs to be represented independently from protocol selection.

Mutual TLS is useful defense in depth for the local IPC boundary but is not proof of user authorization and must not affect the signed-intent trust model. The target design supports plaintext local IPC when TLS is explicitly disabled, ordinary server-authenticated TLS, and optional mutual TLS when client-certificate validation is configured.

## Target state

The stabilized implementation should preserve the current component boundaries while enforcing the following contracts.

### Security and signed intents

- A mutating request is accepted only when the daemon verifies a signature from its operator-managed trust set. ASP authentication, ASP state, and IPC reachability are never mutation authority.
- The signed representation covers the exact operation, complete normalized mutation semantics, signer identity, freshness/replay material, protocol version, and a stable daemon/deployment scope.
- Signed-intent validity has one unambiguous interval. Replay records remain authoritative for the entire interval during which the corresponding signature can still be accepted.
- Replay consumption is durable before privileged execution begins. Restarting the daemon cannot make an already consumed authorization usable again.
- Corrupt or unreadable security state fails closed. The daemon does not silently discard replay or trust information and then continue authorizing mutations.
- Malformed, unauthorized, stale, replayed, or semantically invalid mutations produce deliberate protocol errors without reaching UFW.
- The authorization mechanism remains operation-agnostic enough to support future mutation types without duplicating the cryptographic protocol.

The signed-intent protocol has not shipped upstream yet. Stabilization should correct the existing v1 contract directly rather than preserve compatibility with known-defective interim semantics.

### Authoritative semantic rule model

UFW remains the only source of truth for rule existence. Client-visible rule identities are derived from the daemon's semantic interpretation of current UFW state and never from numbered-list positions or ASP-maintained records.

The semantic model must have a one-to-one relationship with supported UFW operations:

- fields included in semantic identity must affect the represented UFW rule;
- fields accepted from a signed request must not be silently discarded or reinterpreted during command construction;
- inbound and outbound interface constraints must use unambiguous ingress/egress semantics;
- route/forward rules may express both ingress and egress interfaces where UFW supports them;
- semantically equivalent address and port representations normalize to the same identity;
- comments remain non-semantic metadata unless a future design deliberately changes that contract.

IPv4 and IPv6 are both supported by listing, normalization, identity, Add, and Delete. Parsed rows must be consumed completely before they are considered safe semantic state. Unsupported or malformed rows remain visible as raw read-model entries but are not assigned a mutable semantic identity.

Out-of-band changes remain expected. A Delete operation resolves the signed semantic target against fresh UFW state while holding the daemon execution gate. Missing or ambiguous matches fail safely rather than deleting whatever currently occupies an old row number. Short-lived races caused by truly external UFW processes remain outside the guarantees of this phase; daemon-internal races are not.

### Privileged process lifecycle

- The daemon exclusively controls the executable and every argument passed to UFW. No shell interpretation is involved.
- Domain validation and semantic-to-command translation agree on the supported UFW surface.
- Once a mutation child process starts, the daemon retains ownership of that process until it has definitely exited or has been deliberately terminated and reaped. Request cancellation cannot release the execution gate while a UFW mutation is still running.
- UFW output is produced under a deterministic locale suitable for the parser. Standard output and standard error retain clear roles so diagnostic output cannot accidentally become rule-list input.
- Mutation success is reconciled against authoritative post-operation state. If UFW reports success but the daemon cannot establish the expected resulting state, the response represents that uncertainty instead of reporting an ordinary confirmed success.

### Optional TLS and mTLS

Transport encryption is independently configurable from TLS protocol selection:

- TLS can be explicitly disabled for deployments that rely on local IPC permissions alone.
- When TLS is enabled, `SslProtocols.None` retains its .NET meaning: negotiate using the runtime/OS-selected supported protocol set.
- Administrators may explicitly constrain `SslProtocols` when required by deployment policy.
- Server authentication is required whenever TLS is enabled.
- Client authentication is optional. When configured, the server requires and validates an allowed client certificate and the IPC client presents its configured certificate.
- mTLS remains defense in depth. A client certificate authenticates the IPC peer, not the end-user mutation intent, so daemon-side signed-intent verification remains mandatory.

The configuration model should make the enabled/disabled state explicit rather than overloading `SslProtocols.None` with two incompatible meanings.

### Documentation

The permanent documentation should describe the resulting steady-state architecture at three levels:

- `docs/architecture.md`: component responsibilities, rule-management flow, authoritative state, and the relationship between HTTP, IPC, daemon authorization, and UFW;
- `security/architecture-baseline.md`: trust boundaries, signed-mutation invariants, replay/deployment scoping, key ownership, and the defense-in-depth role of TLS/mTLS;
- `docs/protocols/signed-intent.md`: the concrete signed-intent wire/canonicalization contract and operator-facing protocol configuration.

Implementation sequencing, defects in the model baseline, and temporary migration notes should not remain in those steady-state documents.

## Phased implementation plan

Each phase should be implemented on a fresh branch from the latest approved baseline and merged only after review. Tests should accompany the contract they protect rather than being deferred to a final cleanup phase.

### Phase 1: signed-intent and replay correctness

Stabilize the authorization boundary before changing rule semantics.

Work:

1. Add stable daemon/deployment scope to the signed intent and make that scope available to clients through an unsigned daemon/read context suitable for future browser signing.
2. Define one exact freshness interval and use the same expiration semantics in signature validation and replay retention.
3. Harden persistent nonce consumption so a successful consume is durable before UFW execution; reject replay-state corruption or I/O failure closed.
4. Treat malformed JSON/payload data as deliberate client errors rather than framework/internal-server failures.
5. Tighten authorized-key loading where appropriate, including rejecting unsupported key types and avoiding accidental reliance on private-key material.
6. Add focused tests for operation/payload/deployment substitution, validity boundaries, concurrent replay, actual store recreation/restart, corrupt replay state, and malformed payloads.

Exit criteria:

- the same authorization cannot cross the mutation boundary twice, concurrently or after restart;
- an authorization for one deployment is invalid for another deployment;
- all rejected authorization paths have no UFW side effects;
- the daemon remains AOT-compatible.

### Phase 2: semantic rule model, identity, and IPv6

Make semantic identity a faithful representation of every rule the mutation API claims to support.

Work:

1. Replace or constrain the current source/destination interface representation so it models UFW ingress/egress semantics without fallback precedence. Inbound rules accept only the meaningful ingress interface, outbound rules only the meaningful egress interface, and route/forward rules may carry both.
2. Normalize IPv4 and IPv6 hosts/CIDRs semantically, including canonical network addresses for prefixes and equivalent representations of the all-addresses case.
3. Normalize port expressions as semantic values rather than lexical strings, with deterministic ordering and duplicate handling.
4. Extend the parser combinators, syntax nodes, visitor/mapping layer, validation, identity, and command builder to cover the supported IPv6 forms emitted by `ufw status numbered`.
5. Require complete row consumption before a parsed row receives semantic identity. Preserve unsupported rows as raw/unaddressable read data.
6. Verify round trips from supported UFW output -> normalized semantic rule -> identity and from signed semantic rule -> UFW argv -> representative listed state.
7. Add fixtures for IPv4/IPv6, CIDR equivalence, interface direction, forwarding, duplicate semantics, comments, malformed/partially supported rows, and out-of-band reordering/deletion scenarios.

Exit criteria:

- every accepted semantic field has one defined effect on UFW execution;
- equivalent rules produce the same identity across supported IPv4 and IPv6 textual forms;
- Add rejects semantic duplicates and Delete never depends on stale UFW numbering;
- unsupported output cannot accidentally become a mutable parsed rule.

### Phase 3: UFW process and mutation lifecycle

Stabilize the privileged side-effect boundary once rule semantics are fixed.

Work:

1. Define child-process cancellation semantics. After process start, keep the execution gate until the UFW process has exited or has been terminated and reaped; do not allow cancellation to orphan a mutating child behind a released gate.
2. Set a deterministic locale for UFW invocation and separate parseable stdout from diagnostic stderr.
3. Make process start, exit-code, cancellation, and abnormal-exit failures map to explicit daemon responses without pretending an unknown mutation result is a confirmed success.
4. Strengthen post-mutation reconciliation, especially Add, so a successful response means the expected authoritative state was observed.
5. Test subprocess invocation entirely through the existing abstraction/Moq, including cancellation after start, unsuccessful exit, unexpected output, and reconciliation failures.

Exit criteria:

- no daemon-internal ordering can allow two UFW mutations to overlap unintentionally;
- cancellation cannot leave an unowned UFW mutation executing;
- successful mutation responses correspond to confirmed authoritative state;
- command construction remains shell-free and injection-safe.

### Phase 4: optional TLS and mutual TLS

Separate transport-hardening semantics from mutation authorization.

Work:

1. Introduce an explicit TLS enable/disable setting on both daemon and IPC client configuration rather than treating `SslProtocols.None` as disabled.
2. Preserve automatic TLS protocol negotiation when TLS is enabled and `SslProtocols.None` is selected; continue supporting explicit protocol restrictions.
3. Validate server certificates on the client whenever TLS is enabled.
4. Support optional client-certificate presentation and daemon-side client-certificate validation for mTLS.
5. Validate incompatible/partial certificate configuration early and fail startup/configuration clearly; server certificate paths are required only when TLS is enabled, and client certificate material is required only when mTLS/client authentication is configured.
6. Add transport tests for explicitly disabled TLS/plaintext IPC, automatic-protocol TLS, explicitly constrained TLS, mTLS success, missing client certificate, and invalid/untrusted client certificate.

Exit criteria:

- TLS enablement is explicit and orthogonal to protocol selection;
- `SslProtocols.None` has standard .NET auto-selection behavior when TLS is enabled;
- mTLS is optional and cannot replace signed-intent authorization.

### Phase 5: documentation and integration cleanup

Once the behavioral contracts are stable, rewrite the permanent documentation as a coherent steady-state description.

Work:

1. Reconcile `docs/architecture.md`, `security/architecture-baseline.md`, and `docs/protocols/signed-intent.md` with the final implementation and remove transitional baseline wording.
2. Keep architecture, security invariants, and wire-level protocol detail at their respective abstraction levels; replace duplicated explanations with cross-links where appropriate.
3. Document IPv4/IPv6 rule identity semantics, out-of-band state behavior, mutation failure/uncertainty boundaries, daemon-owned trust/replay state, and optional TLS/mTLS.
4. Update README/configuration examples and remove stale build or solution references encountered during the pass.
5. Review the complete documentation set for terminology, links, examples, and contradictions.

Exit criteria:

- a new contributor can understand the browser/ASP/IPC/daemon/UFW trust and data flow without reading implementation history;
- security and protocol documentation describe the same invariants without duplicating unrelated implementation detail;
- no permanent document describes `SslProtocols.None` as disabling TLS or IPv6 as inherently unaddressable.

## Deferred concerns

The stabilization work deliberately does not expand into:

- browser-side private-key storage or signing UX;
- dynamic authorized-key enrollment, revocation APIs, or user/key lifecycle management;
- ASP-side rule reconciliation, semantic/reachability analysis, or firewall metadata persistence;
- audit logging and accountability infrastructure;
- EditRule or broader firewall mutation types beyond ensuring the authorization design remains reusable;
- cross-process locking against administrators or unrelated programs invoking UFW concurrently.

Those concerns can build on the stabilized contracts without weakening the current separation between application authorization and privileged firewall authority.
