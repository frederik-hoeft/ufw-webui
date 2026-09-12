# Ordered Rule Insertion Implementation Plan

> **Status:** Implementation in progress. Phases 1 and 2 are approved; Phase 3 is at its review gate. This document defines the review boundaries for the ordered rule insertion feature while implementation is in progress. Permanent behavior belongs in `docs/architecture`, `docs/protocols`, `docs/security`, and `docs/deployment` after the feature reaches its approved steady state.

Ordered insertion reuses the exact-snapshot authority model established for rule reordering but remains a distinct mutation. The browser signs both the new structural rule and its placement relative to one occurrence in an exact authoritative baseline. The daemon independently validates that baseline and placement before executing one UFW insertion. It does not reinterpret ordered insertion as a reorder followed by an append, and the Web layer never gains mutation-planning authority.

The initial contract intentionally requires a **concrete address family** for the new rule. A family-neutral UFW add may materialize into both IPv4 and IPv6 rows, while a single snapshot occurrence identifies one concrete ordered position. Supporting dual-family ordered creation would therefore require richer placement semantics and is outside this implementation. The ordered-create flow derives and locks the new rule family from the selected anchor occurrence; normal append-style creation retains the existing family-neutral behavior.

Implementation is split into four independently reviewable phases.

## Phase 1: signed contract and exact placement semantics

Establish the side-effect-free primitives shared by the browser, Web transport, and daemon.

This phase defines the `rules.insert` operation, signed payload, canonical byte representation, and placement vocabulary. The payload binds the normalized structural rule, exact baseline fingerprint, zero-based anchor occurrence ID, and `before`/`after` placement. It also defines pure placement validation: the baseline must identify a parsed concrete-family anchor, the inserted rule must use that same concrete family, and an anchor occurrence is meaningful only relative to the signed baseline fingerprint.

The phase does not expose a daemon route, invoke UFW, or enable the existing ordered-create prototype.

**Review gate:**

- changing the rule, baseline fingerprint, occurrence ID, placement, deployment identity, nonce, or operation changes the signed canonical bytes;
- malformed fingerprints, negative/out-of-range occurrence IDs, unknown placement values, family-neutral inserted rules, and family mismatches are rejected before privileged execution;
- duplicate semantic rules remain independently addressable because placement uses snapshot-local occurrences rather than `ruleId`;
- source-generated serialization covers the new request/payload without reflection fallbacks;
- existing add/delete/reorder contracts remain behaviorally unchanged;
- full build and regression suites remain green.

## Phase 2: daemon execution and reconciliation

Implement the privileged single-mutation transaction under the existing UFW execution gate.

The daemon verifies the exact baseline, validates interfaces and duplicate semantics using the existing add-rule machinery, resolves the signed combined-snapshot occurrence into UFW's concrete family-local insertion number, executes one mutation, and reconciles the complete authoritative post-state. `before` inserts at the selected occurrence. `after` inserts before the next occurrence in the same address-family partition, or appends that concrete-family rule when the selected occurrence is the last rule in its family.

The existing reorder mutation-safety guard runs before ordered insertion so an unresolved delete/reinsert recovery obligation blocks all later mutations. Ordered insertion itself needs no recovery journal because it never removes an existing row.

**Review gate:**

- baseline mismatch causes a no-mutation stale-state result;
- interface/semantic duplicate/precondition validation completes before UFW starts;
- before/after placement is correct at the beginning, middle, and end of both concrete family partitions;
- cancellation/process failure after UFW may have started triggers non-cancelable authoritative reconciliation rather than assuming failure or success from the exit status alone;
- success requires the exact postcondition: all baseline occurrences remain in relative order, exactly one requested rule materialization was added, and it occupies the signed placement;
- unexpected out-of-band changes are detected and reported without attempting unrelated corrective mutations;
- real command arguments and `Ufw.Mock` agree on positional insertion behavior.

## Phase 3: IPC and Web transport

Expose ordered insertion through the existing browser-to-Web-to-daemon signed mutation path.

This phase adds daemon intent verification/replay handling, typed IPC bindings, daemon routing, Web forwarding, REST response mapping, and serialization tests. The REST surface is a dedicated `POST /api/v1/rules/insert` endpoint so ordered membership mutation cannot be confused with append-style `POST /rules` or order-only `PUT /rules/order`.

Web authenticates the caller and forwards the signed request. The daemon remains the sole authority for baseline validation, occurrence interpretation, placement calculation, replay protection, and UFW execution.

**Review gate:**

- tampering with any state-conditioned or rule field invalidates the signature;
- nonce replay remains rejected sequentially, concurrently, and after restart persistence;
- typed stale/precondition/uncertain outcomes survive IPC and REST without being flattened into ambiguous generic failures;
- REST and daemon routing expose only the dedicated insertion endpoint;
- append add, delete, and reorder transports remain behaviorally unchanged;
- production serialization and in-process IPC integration cover the new request/result types.

## Phase 4: browser enablement, end-to-end hardening, and steady-state documentation

Replace the disabled ordered-insertion prototype with the production flow and close the feature with production-equivalent integration coverage.

The Rules page, which owns the authoritative snapshot, converts a row-menu before/after action into snapshot-local placement context rather than placing a semantic `ruleId` in the URL. The Create page retains that exact baseline, locks address family to the selected concrete anchor, signs the normalized rule plus placement at submission time, and requires a fresh authoritative review when the baseline becomes stale. The existing rule editor, signing-key workflow, mutation error model, and authoring validation are reused wherever their contracts already fit.

The final phase exercises browser signing through Web/IPC/daemon execution against `Ufw.Mock` at the external UFW boundary, then promotes approved semantics into permanent documentation and removes the temporary ordered-insertion backlog item.

**Review gate:**

- duplicate semantic anchor rows can be targeted independently by occurrence;
- ordered creation cannot silently fall back to append when the signed baseline/anchor is stale or unavailable;
- family selection is locked to the concrete anchor for ordered insertion while ordinary append creation keeps existing family-neutral behavior;
- success, stale-state, rejected, and uncertain-outcome UX preserve authoritative refresh requirements and clear signing-key state appropriately;
- production-equivalent integration covers before/after insertion, family-boundary placement, replay, stale baseline, and out-of-band divergence;
- permanent protocol/security/firewall-model documentation describes the implemented steady-state contract coherently;
- full isolated offline restore, build, tests, formatting, localization consistency, and documentation-link validation are clean.
