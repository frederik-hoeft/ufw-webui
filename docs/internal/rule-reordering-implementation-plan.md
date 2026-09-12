# Firewall Rule Reordering Implementation Plan

> **Status:** Active implementation plan. Each phase ends at a review gate and is merged independently after approval. The target architecture is described in [Firewall Rule Reordering Design](rule-reordering-design.md).

Implementation is split along architectural boundaries so that protocol semantics, privileged execution, transport integration, and browser behavior can be reviewed independently. A later phase may refine an earlier internal abstraction when implementation exposes a constraint, but changes to an already approved phase should remain explicit rather than being hidden inside unrelated work.

## Phase 1: contracts, canonical state, and planning

Establish the pure, side-effect-free primitives on which every later layer depends.

This phase defines the signed reorder payload and operation identifier, the canonical authoritative-snapshot fingerprint shared by browser and daemon, and the deterministic reorder planner. The planner operates only on snapshot-local occurrence IDs. It validates source/target permutations and immutable anchors, selects a maximum untouched subsequence using an LIS-based algorithm, and emits a globally minimal set of logical moves under the unit-cost remove/reinsert model.

The phase does not expose a daemon route, execute UFW, alter the browser preview flow, or persist recovery state.

**Review gate:**

- canonical snapshot hashing is deterministic, domain-separated, and covered by fixed-vector tests;
- any represented firewall-state change that matters to reorder authority changes the fingerprint;
- `rules.reorder` canonical signing binds both baseline fingerprint and complete desired occurrence permutation;
- planner output reaches the requested permutation, never moves an immutable occurrence, and uses the global minimum number of moves;
- malformed/mismatched permutations and impossible immutable-anchor orderings fail before a plan is produced;
- exhaustive small-permutation tests validate minimality and deterministic behavior;
- full build and existing regression suites remain green.

## Phase 2: daemon execution and move recovery

Implement the privileged reorder transaction behind an internal daemon service while retaining the global UFW execution gate for the complete operation.

This phase introduces reinsertability classification, positional UFW insertion, preflight rendering, logical delete/reinsert moves, authoritative reconciliation after each subprocess, structured partial-result classification, and the durable single-move recovery journal. Reinsertability risk also supplies the planner's secondary keep-priority so equally minimal plans preferentially leave higher-risk movable rows untouched. Startup mutation readiness incorporates recovery of an incomplete journal before accepting new firewall mutations.

The daemon must stop planned execution on unexpected divergence after making the active move safe. Once deletion may have taken effect, caller cancellation cannot abandon recovery.

**Review gate:**

- no mutation begins before baseline/permutation/reinsertability preflight succeeds;
- every logical move preserves the row-presence recovery invariant across success, subprocess failure, cancellation, and simulated restart;
- unexpected state divergence prevents subsequent planned moves;
- final/partial reports accurately describe authoritative post-state and remaining safe work;
- execution remains serialized with all existing UFW reads and mutations;
- real command arguments and the mock UFW implementation agree on positional insertion semantics;
- failure-injection tests cover each delete/insert/reconciliation boundary.

## Phase 3: signed intent, IPC, and REST transport

Expose reorder through the existing browser-to-Web-to-daemon request path without moving authorization or planning authority into `Ufw.Web`.

This phase adds operation-specific daemon intent verification, nonce handling, typed IPC request/response bindings, daemon controller routing, Web REST forwarding, and structured HTTP result mapping. The Web layer remains a transport/authentication boundary and forwards the signed payload without interpreting occurrence identities or constructing move plans.

**Review gate:**

- modified baseline fingerprint, desired permutation, nonce, operation, or deployment identity invalidates the signature;
- replay remains rejected across sequential/concurrent calls and restart persistence;
- stale baseline reaches the daemon and returns a structured no-mutation report;
- IPC production serialization and in-process protocol integration cover every new request/result type;
- REST preserves partial/recovery diagnostics rather than collapsing them into generic errors;
- existing add/delete flows remain behaviorally unchanged.

## Phase 4: browser preview, signing, and result UX

Replace the provisional frontend mock boundary with the production reorder flow while preserving the current local preview interaction.

The preview model uses baseline occurrence IDs rather than semantic `ruleId` values. Apply computes the fingerprint from the exact displayed authoritative snapshot, signs one complete desired permutation, submits it through the real API client, and replaces local authority with the daemon-returned final snapshot. Partial/recovered outcomes are presented as an execution report; refresh or authoritative replacement invalidates the old occurrence namespace.

**Review gate:**

- duplicate semantic rules can be previewed/reordered without identity ambiguity;
- browser signing uses the displayed baseline rather than a Web-provided opaque digest;
- drag/drop history is not part of the privileged request contract;
- stale/partial/recovery outcomes replace local authoritative state and communicate applied vs pending work;
- no automatic retry/rebase reuses the original signature or nonce;
- add/delete remain disabled while an ordering preview is active;
- client service/component tests cover preview lifecycle and error paths.

## Phase 5: end-to-end hardening and steady-state documentation

Exercise the complete feature against production-equivalent IPC, Web integration, `Ufw.Mock`, and representative real-UFW syntax fixtures. Resolve any remaining lossless-reinsertion blockers before enabling affected rule shapes.

Once behavior is stable, promote the approved wire semantics and lifecycle from the internal design into the permanent protocol, firewall-model, security, operations, and development documentation. Internal task/design status is updated so it no longer competes with the steady-state architecture documentation.

**Review gate:**

- end-to-end tests cover complete reorder, stale baseline, partial execution, recovery, replay, duplicates, opaque anchors, IPv4/IPv6 materializations, and out-of-band divergence;
- supported movable rule shapes round-trip without semantic loss;
- no enabled path claims packet-level atomicity beyond the sequential UFW CLI guarantee;
- permanent docs describe the implemented steady-state behavior coherently;
- full offline restore/build/test/format validation is clean.
