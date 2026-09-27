# Rule editing and replacement implementation plan

This document is the active implementation plan for first-class firewall-rule editing. It refines the backlog item in [backlog.md](backlog.md) against the current implementation and records the review boundaries for the work.

The plan is intentionally temporary. Once the implementation is approved, its durable contracts belong in the permanent architecture, protocol, security, development, and testing documentation; this file and the completed backlog item should then be removed.

## Goal

Add an **Edit rule** workflow that lets an administrator start from one exact live rule occurrence, edit its structural rule definition and application-owned metadata through the existing authoring controls, sign the structural replacement, and preserve the rule's family-local position.

Editing is a state-conditioned replacement operation rather than an in-place mutation. The browser signs the exact authoritative baseline it reviewed, the target occurrence inside that baseline, the target's expected semantic `RuleId`, and the complete replacement rule. The daemon remains the firewall authority and returns an authoritative final snapshot after execution whenever that state can be read reliably.

The implementation must preserve the existing trust boundaries:

- UFW remains authoritative for firewall state.
- Browser-signed intent authorizes every firewall mutation.
- ASP forwards firewall intent and reconciles only application-owned metadata.
- PostgreSQL metadata never becomes firewall authority.
- Snapshot-local occurrence IDs, not semantic hashes alone, identify one row when duplicate semantic rules exist.

## Current-state inventory

### Browser authoring and rule-list UI

The existing authoring surface is already split at useful boundaries:

- `RuleEditor` owns the structural rule form, validation integration, known-host/interface suggestions, canonical UFW preview, and mutation authorization input.
- `RuleMetadataEditor` owns notes, tags, and group selection independently of the structural rule.
- `IRuleEditorValidationService`, `IRuleEditorReferenceDataService`, and the presentation/localization services keep reusable domain behavior out of Razor markup.
- `IRuleDraftFactory` creates a mutable default rule draft.
- `IRuleMutationService` composes intent-context acquisition, signing, and typed rule API calls.
- `IRuleMutationReconciliationService` contains identity/post-mutation reconciliation helpers.
- `IRuleInsertionNavigationService` demonstrates the existing pattern for carrying a snapshot fingerprint plus an exact occurrence through navigation and resolving that context only against the matching authoritative snapshot.

`CreateRule` currently orchestrates both append creation and ordered insertion. Its code-behind owns inventory refresh/staleness state, insertion-context resolution, signing/mutation calls, post-firewall metadata persistence, and navigation. Those responsibilities are specific enough that a second page should not copy the implementation wholesale, but they also should not be collapsed into a broad page-manager/facade merely to share code. The edit workflow can reuse the existing editor and metadata components directly, with focused feature services extracted only for state-independent navigation/context and replacement reconciliation behavior.

The rule-list projection currently exposes two different capabilities through `CanOrder` and `CanMutate`:

- `CanOrder` requires a parsed structural rule and therefore works for duplicate semantic occurrences.
- `CanMutate` additionally requires a unique `RuleId`, because legacy single-rule deletion identifies the target semantically.

Replacement should remain occurrence-aware, but pre-existing duplicate semantic identities are operationally invalid firewall state rather than a first-class authoring scenario. The projection should expose an explicit edit/replace capability for parsed rows with a concrete rule and semantic identity while preserving `CanMutate` for the existing delete path. The replacement workflow may naturally support some duplicate-target cases where the exact occurrence remains unambiguous, but it must fail closed before mutation whenever duplicate state makes the operation unsafe or ambiguous.

`RuleActionsMenu` already has distinct ordering/insertion, metadata-edit, and delete actions. **Edit rule** belongs beside **Edit metadata** as a separate operation.

### Signed-intent and shared contracts

Signed-intent protocol v2 already supports adding new operation-specific payloads without changing the protocol version. The relevant reusable machinery is:

- `IntentOperations` for operation names;
- `IntentCanonicalizer` for deterministic field framing;
- operation-specific contract validators such as `RuleInsertionContract`;
- shared request/response DTOs and the source-generated IPC JSON context;
- `IntentVerifier` plus typed accepted verification results;
- browser-side `IIntentSigningService` / `BrowserIntentSigningService`.

Ordered insertion, batch deletion, and reorder establish the correct model for state-conditioned authority: sign a complete snapshot fingerprint and interpret occurrence coordinates only after the daemon has reproduced that exact snapshot under the shared mutation gate.

A replacement payload should therefore contain:

- `BaselineFingerprint`: fingerprint of the exact reviewed `RuleListResponse`;
- `TargetOccurrenceId`: zero-based occurrence in that baseline;
- `OriginalRuleId`: semantic identity expected at that occurrence;
- `ReplacementRule`: complete normalized structural rule proposed by the signer.

The original full rule does not need to be duplicated in the payload because the fingerprinted occurrence already commits to it, but `OriginalRuleId` is still useful as an explicit signed semantic precondition and as the metadata-reconciliation identity.

### Daemon mutation pipeline

The daemon already provides the primitives needed for replacement:

- `IFirewallRuleSnapshotReader` produces the authoritative pre/post snapshots.
- `IFirewallMutationExecutionGate` serializes firewall mutation execution.
- `IFirewallMutationSafetyGuard` prevents unrelated mutations while an interrupted reorder recovery remains unresolved.
- `IFirewallRuleCapabilityValidator` and `IFirewallRuleInterfaceValidator` enforce add-style runtime preconditions.
- `UfwRulePositionResolver` translates a snapshot occurrence/family position to UFW's combined numbered coordinate.
- `UfwInsertRuleCommand`, `UfwAddRuleCommand`, and the numbered-delete path provide the actual UFW operations.
- `FirewallOrderedInsertionExecutor` demonstrates exact baseline validation and exact postcondition matching.
- batch deletion and reorder demonstrate structured partial/state-uncertain result models rather than pretending multi-command changes are atomic.

The existing ordered-insertion executor should not be called as a black box after deletion: its signed anchor semantics and duplicate preconditions describe a different user operation. Replacement should have its own small executor and reuse the lower-level snapshot, position, validation, command-rendering, and comparison primitives.

The existing reorder recovery journal is also not directly reusable. Its invariant is "a removed rule must eventually be present again" because reorder changes only position. A replacement has two distinct structural states, old and new, so blindly applying reorder recovery after an uncertain replacement could restore the old rule even when the replacement actually succeeded. If durable replacement compensation is introduced later, it needs a replacement-aware recovery record rather than a renamed reorder journal.

### ASP metadata ownership

`RuleMetadataService` and `IRuleMetadataRepository` already isolate application-owned rule metadata from firewall state. Batch deletion provides the relevant precedent: reconcile metadata only from a daemon result that contains an authoritative final snapshot, and never infer firewall success from the browser's intended mutation.

Replacement adds one new transactional repository operation because metadata may need to move between semantic identities while preserving notes, tags, and group membership:

- unchanged identity: no metadata move;
- old identity still live after a successful replacement: keep the old row and associate equivalent metadata with the new identity;
- old identity no longer live: migrate/re-key the existing metadata row to the new identity;
- stale metadata already exists under the new identity: the reconciliation transaction must resolve that collision deterministically rather than failing halfway through the move. The metadata attached to the rule being edited is the source of truth for the replacement association.

The repository should implement copy/re-key semantics transactionally. The controller should not open-code EF mutations or try to infer duplicate occurrence behavior itself.

### Existing validation baseline

The supplied 2026-09-27 source snapshot restores completely from the supplied offline package set with .NET SDK 10.0.400. Targeted baseline validation is clean:

- `Ufw.Shared.Tests`: 89/89 passed;
- `Ufw.Systemd.Tests`: 282/282 passed;
- `Ufw.Ipc.Tests`: 141/141 passed;
- `Ufw.Web.Tests`: 265/265 passed;
- `Ufw.Web.Client.Tests`: 278/278 passed;
- targeted `Ufw.Web.Tests` and `Ufw.Web.Client.Tests` builds complete with zero warnings/errors.

A full solution build also reached the shared, IPC, daemon, mock, and systemd test projects without a source diagnostic before the sandbox command timeout; targeted builds establish the relevant baseline independently of that environment-duration limit.

## Proposed replacement contract

### Signed operation

Add a `rules.replace` signed-intent operation and a `PUT /api/v1/rules/replace` daemon/ASP route. The new request remains a normal signed-intent envelope whose payload is the replacement contract above.

The shared contract validator should reject malformed fingerprints/occurrences, malformed or mismatched original identities, non-concrete replacement families, and structural rules that fail the existing rule validator. Resolution against a snapshot additionally requires:

1. `TargetOccurrenceId` exists in the signed baseline;
2. the target is parsed and has a concrete address family;
3. the target's effective semantic identity equals `OriginalRuleId`;
4. the replacement has the same concrete address family as the target.

Address-family changes are intentionally outside first-class editing. Position is family-local, so "preserve this rule's position" has no unambiguous meaning when moving between the IPv4 and IPv6 partitions. The edit UI should lock the address-family selector. A future cross-family move can be designed as an explicitly different workflow rather than smuggling delete/create semantics into replacement.

### Duplicate semantics

The workflow is occurrence-addressed so it never mistakes a semantic hash for a unique row identity. However, duplicate semantic rules in UFW are treated as pre-existing invalid/unsafe firewall state, not as a compatibility requirement the editor must normalize or repair.

The daemon may support a duplicate-target case only when that support falls out naturally from the existing snapshot/position model and the complete mutation remains unambiguous. Otherwise it must reject the edit before starting any UFW process and require an administrator to repair the firewall state first. In particular, a same-semantic-identity update must be rejected when the target identity occurs more than once because UFW's existing-rule update semantics cannot reliably select one duplicate occurrence.

The replacement may keep the same semantic identity as a unique target. This is required for changes such as comment-only edits because comments are intentionally excluded from `RuleId`.

When the replacement computes a different semantic identity, the daemon must reject the operation if that new identity already exists anywhere in the authoritative baseline. This check happens before mutation and prevents the editor from creating a new semantic duplicate or entering a partial state because UFW refuses the insertion.

### Execution and outcomes

Replacement should use the same execution gate, mutation-safety guard, nonce consumption, capability checks, and interface checks as adjacent signed mutations.

Replacement should minimize the interval in which the known-good original rule is absent. The execution path depends on whether the semantic identity changes:

1. read the authoritative snapshot under the mutation gate;
2. require an exact fingerprint match and resolve the exact target occurrence;
3. reject every unsafe/ambiguous duplicate condition and every replacement identity that already exists elsewhere before starting a UFW process;
4. if the normalized structural model and comment are unchanged, complete as a firewall no-op;
5. if the semantic identity is unchanged but the comment changes, require that identity to be unique and use UFW's existing-rule update semantics, including explicit empty-comment rendering when removing a comment, then require the exact expected snapshot;
6. if the semantic identity changes, insert the replacement immediately before the exact target occurrence, then require the exact intermediate snapshot containing both the new and old rule;
7. only after the replacement is confirmed present, delete the old exact occurrence and require the exact final replacement snapshot.

For identity-changing edits, insert-first is the safer ordering because a failed insertion leaves the original rule intact. If the subsequent delete fails while the exact intermediate state is still confirmed, the daemon should make a best-effort attempt to remove the newly inserted replacement and restore the exact baseline. Recovery is explicitly best effort: UFW is externally mutable and the daemon cannot promise atomicity. If authoritative state diverges from a state the daemon can classify safely, it must stop issuing further mutation commands and report uncertainty rather than guessing.

The same-identity path deliberately does not use insert/delete. UFW treats a semantically identical add as an update opportunity for the existing rule comment, while `insert`/`prepend` cannot reliably express occurrence-specific updates. This avoids deleting a valid rule merely to change application-visible UFW comment text.

Do not add durable replacement journaling in the first implementation. A later recovery design can be added explicitly if operational evidence justifies it, but it must be replacement-aware rather than reusing reorder's "ensure old rule exists" journal.

The response should carry an operation outcome, authoritative final snapshot when known, the confirmed replacement row on success, recovery information when relevant, and a diagnostic. The minimum outcome model is:

- `Completed`: the exact requested postcondition is confirmed, including a confirmed no-op/update path where applicable;
- `StaleBaseline`: no mutation started because the signed snapshot no longer matches;
- `PreconditionFailed`: no mutation started because target/replacement/runtime or duplicate-safety preconditions failed, or a started command is confirmed to have left/restored the baseline unchanged;
- `PartiallyCompleted`: the requested replacement did not complete, but the daemon has a reliable authoritative snapshot that differs from both the requested final state and the exact baseline;
- `StateUncertain`: the daemon cannot establish a trustworthy final firewall snapshot after a process may have started or observes a state that cannot be classified safely.

As with batch deletion/reorder, a non-success outcome may still carry a final snapshot. The browser must require a fresh authoring context before another replacement attempt after stale, partial, or uncertain execution.

## Metadata reconciliation contract

ASP reconciles metadata only after a `Completed` firewall replacement with an authoritative final snapshot and confirmed replacement row. Partial or uncertain firewall outcomes leave metadata untouched; preserving an orphaned old metadata row is preferable to destructively guessing where metadata should move when no successful replacement identity has been proven.

For a completed replacement:

1. determine `newRuleId` from the daemon-confirmed replacement row;
2. if `newRuleId == OriginalRuleId`, do nothing to the metadata key;
3. otherwise, inspect the final snapshot for any remaining occurrence of `OriginalRuleId`;
4. if the old identity remains live, copy the old metadata values to `newRuleId` while retaining the old row;
5. if the old identity is absent, migrate/re-key the old metadata row to `newRuleId`;
6. perform copy/re-key/collision handling in one repository transaction.

The browser edit form can still let the user change metadata at the same time as the structural edit. Structural replacement and automatic metadata migration happen first; then, if the metadata draft differs from the values loaded for the edited rule, the browser uses the existing metadata update endpoint against the confirmed new identity. If that optional metadata update fails, the automatically migrated/copied original metadata remains associated with the replacement and the UI can report only the metadata edit failure without misrepresenting firewall success.

ASP-side metadata-reconciliation failure is a separate application failure from firewall replacement. It must be logged and surfaced to the browser without rewriting the daemon's firewall outcome. Phase 4 should choose the smallest ASP-owned response wrapper/status representation that preserves both facts; do not overload `RuleReplacementOutcome` with PostgreSQL state.

## Browser workflow

Selecting **Edit rule** from a row should navigate to a dedicated `/rules/edit` page with snapshot-local context, analogous to ordered insertion. A focused replacement-navigation service should build/resolve:

- baseline fingerprint;
- target occurrence ID;
- original semantic `RuleId`.

The edit page reloads the authoritative inventory and resolves the context only if the fingerprint and target still match. It then:

- clones the live structural rule into an independent mutable draft;
- loads the metadata already projected for the semantic identity into `RuleMetadataEditor`;
- locks address family;
- reuses `RuleEditor` validation, suggestions, canonical rendering, and authorization input;
- clearly presents both the current canonical rule and proposed canonical replacement before signing;
- submits through `IRuleMutationService` and the new signed replacement operation;
- clears private-key material after submission;
- treats stale/partial/uncertain results as invalidating the edit context;
- on completion, uses the daemon-confirmed replacement identity for any user-requested metadata update and returns to the ordinary rules inventory.

`RuleEditor` should gain an explicit create/edit presentation mode (or an equally small typed parameter object) so edit-specific current/proposed preview and action icon/labels are part of the reusable authoring component rather than duplicated markup. Ordered insertion remains a create-mode operation with its current page-specific labels.

`IRuleDraftFactory` should gain an explicit "create from existing rule" path that returns an independent normalized mutable copy. Pages should not manually copy every property, which would make future rule-model additions easy to miss.

## Phased implementation and review gates

Each phase is a separate local feature branch from the last approved baseline. It is validated, committed, and returned as a patch; it remains unmerged until explicitly approved. Tests belong with the behavior introduced by each phase rather than being deferred to a final test-only branch.

### Phase 1: reusable edit-authoring foundation

Branch: `feature/rule-edit-authoring-foundation`

Scope:

- formalize create/edit presentation in `RuleEditor` without duplicating the structural form;
- add draft initialization from an existing `FirewallRuleSpecification`;
- add snapshot-bound replacement navigation/query/context resolution as a sibling of ordered insertion;
- expose a distinct row capability for rule editing without changing legacy single-delete semantics; duplicate-state editability must fail closed wherever occurrence-specific behavior is ambiguous;
- add the row capability and event/navigation primitives needed by **Edit rule**, but keep the visible action undiscoverable until the end-to-end edit page exists in Phase 5;
- add focused unit/component-model tests for cloning independence, context resolution, stale/mismatched target rejection, duplicate-row rejection, and navigation primitives.

QA gate:

- targeted client build and `Ufw.Web.Client.Tests` pass offline;
- `git diff --check` passes;
- review confirms that no second copy of rule-field validation/suggestion/form logic was created and no broad page-manager abstraction was introduced.

### Phase 2: replacement signed-intent and transport contract

Branch: `feature/rule-replacement-contract`

Scope:

- add `ReplaceRulePayload`, `ReplaceRuleRequest`, `RuleReplacementOutcome`, and `RuleReplacementResponse` shared DTOs;
- add `rules.replace` canonicalization and contract validation;
- extend source-generated JSON registrations;
- extend daemon `IntentVerifier` with a typed accepted replacement result;
- extend browser intent signing and the typed mutation/API abstractions needed by the later endpoint without exposing a callable replacement route yet;
- add canonicalization, normalization, malformed-contract, signing, verifier/operation-mismatch, serializer, and client-mutation tests.

QA gate:

- `Ufw.Shared.Tests`, intent-focused `Ufw.Systemd.Tests`, IPC protocol tests, and client signing/mutation tests pass offline;
- targeted Shared/Systemd/IPC/Client builds pass;
- review confirms that replacement remains signed protocol v2, binds all state-dependent authority, and does not leak ASP metadata into daemon payloads.

### Phase 3: daemon replacement reconciliation and execution

Branch: `feature/rule-replacement-daemon`

Scope:

- add a focused replacement service/executor under the firewall mutation subsystem;
- run verification, mutation safety guard, nonce consumption, and execution under the shared mutation gate;
- require exact fingerprint/occurrence/original-identity reconciliation before mutation;
- lock replacement to the target address family;
- preserve existing new-duplicate rejection and treat duplicate old identities as best-effort compatibility only: support them only when the exact occurrence operation is naturally unambiguous, otherwise reject before mutation;
- use a no-op or unique existing-rule comment update when semantic identity is unchanged;
- for identity-changing edits, insert the replacement immediately before the exact target, verify the exact intermediate state, then delete the old occurrence;
- if deletion fails while the exact intermediate state is confirmed, attempt best-effort rollback by removing the newly inserted replacement, but stop and report uncertainty on any unclassifiable divergence;
- classify exact success, stale, precondition, restored-baseline failure, partial, and uncertain states from authoritative snapshots;
- wire the daemon controller endpoint and typed IPC response;
- reuse lower-level position/snapshot/command primitives, extracting a narrowly reusable helper only if replacement and ordered insertion otherwise duplicate non-trivial command-position or exact-snapshot logic.

QA gate:

- comprehensive `Ufw.Systemd.Tests` for first/middle/last positions, IPv4/IPv6, same-identity no-op/comment update/comment removal, duplicate-old-identity rejection or naturally supported unambiguous cases, conflicting new identity, stale baseline, target mismatch, interface/capability rejection, insertion failure/no-op, confirmed insertion followed by delete failure, successful and failed best-effort rollback, process-failure-with-confirmed-postcondition, unreadable/divergent final state, cancellation, nonce/replay, and outstanding reorder-recovery blocking;
- relevant IPC integration tests use the UFW mock for end-to-end route/execution coverage;
- full Systemd and IPC test projects pass offline;
- review confirms fail-closed duplicate handling, insert-before-delete semantics for identity-changing edits, same-identity update behavior, and the explicitly best-effort/non-atomic recovery contract before merge.

### Phase 4: ASP metadata reconciliation

Branch: `feature/rule-replacement-metadata`

Scope:

- forward the replacement request through ASP and map daemon outcomes consistently with other state-conditioned mutations;
- add one transactional metadata-repository operation that can no-op, copy, or re-key metadata based on the authoritative completed result;
- handle stale metadata collisions under the new identity deterministically inside that transaction;
- keep metadata unchanged for partial/uncertain replacement outcomes;
- surface ASP metadata-reconciliation failure separately from the already-completed firewall result;
- preserve source metadata values, tags, group membership, and the appropriate public metadata identity when re-keying;
- add controller/service/repository integration coverage for no metadata, same identity, final old occurrence, any naturally supported surviving-duplicate case, target collision, DB failure, and non-completed firewall outcomes.

QA gate:

- targeted `Ufw.Web` build, `Ufw.Web.Tests`, and affected integration tests pass offline;
- review confirms that ASP never chooses the firewall target or claims firewall rollback, and that metadata reconciliation is driven only by the daemon-confirmed final state.

### Phase 5: browser edit workflow and UX

Branch: `feature/rule-edit-workflow`

Scope:

- add the `/rules/edit` page using the phase-1 reusable editor/metadata controls and replacement context;
- initialize structural and metadata drafts from the selected authoritative occurrence;
- show current and proposed canonical commands in edit mode;
- lock address family and disable submission whenever the snapshot/context is stale;
- submit the signed replacement, render partial/uncertain diagnostics, and invalidate stale replacement context after any non-completed state-conditioned outcome;
- after completed replacement, apply user-changed metadata through the confirmed new identity and then refresh/navigate through the ordinary authoritative inventory flow;
- add localization/accessibility/UI-state coverage and keep private-key handling consistent with existing mutations.

QA gate:

- targeted client build and full `Ufw.Web.Client.Tests` pass offline;
- browser-facing tests cover duplicate-state rejection/disabled behavior where applicable, stale-navigation context, same-identity edits, replacement identity changes, metadata update failure after firewall success, and partial/uncertain result presentation;
- manual code review verifies responsive desktop/mobile action plumbing and no optimistic local firewall substitution;
- `git diff --check` passes.

### Phase 6: steady-state documentation reconciliation and final regression pass

Branch: `docs/rule-replacement-steady-state`

Use `dev/technical-documentation.skill` for this phase.

Scope:

- reconcile permanent documentation against the approved implementation rather than narrating the implementation history;
- update at minimum:
  - `docs/architecture/firewall-model.md` for replacement identity, ordering, duplicate, partial, and metadata consequences;
  - `docs/architecture/security.md` and `docs/architecture/architecture-overview.md` for replacement signed authority and trust boundaries;
  - `docs/architecture/browser-application.md` for edit authoring/navigation/reconciliation responsibilities;
  - `docs/protocols/signed-intent.md` for `rules.replace` payload/canonicalization/verification/execution semantics;
  - `docs/protocols/application-protocol.md` for the daemon route and typed outcome semantics;
  - `docs/development/client-ui.md` only where contributor-facing authoring guidance materially changed;
- search the documentation set for stale claims that enumerate supported mutations or describe replacement-incompatible duplicate semantics;
- remove the completed replacement backlog item and delete this temporary plan once its durable decisions are represented in permanent docs;
- run the strongest practical full offline build/test regression and documentation/link/coherence checks.

QA gate:

- permanent docs describe only steady-state behavior and agree with the implementation;
- no completed implementation plan/backlog residue remains under `docs/internal`;
- all relevant test projects and targeted/full builds supported by the environment pass;
- final branch diff is limited to documentation reconciliation and any strictly necessary doc-fix fallout.

## Confirmed implementation decisions

The planning gate confirmed the following policies:

1. **Cross-family edits:** address family is locked. IPv4↔IPv6 conversion is a different future workflow because family-local position cannot be preserved unambiguously across partitions.
2. **Duplicate semantic state:** the editor never creates a new semantic duplicate. Pre-existing duplicate semantic rules are treated as operationally invalid/unsafe state. The implementation may support an exact duplicate-target case only when that support is natural and unambiguous; otherwise it fails closed before mutation and requires administrative repair. Same-identity updates against duplicate targets are rejected.
3. **Replacement ordering and recovery:** same-identity edits use the narrow no-op/existing-rule update path. Identity-changing edits insert and verify the replacement before deleting the original. If deletion fails from the exact known intermediate state, the daemon attempts best-effort rollback, but it makes no atomicity promise and stops mutating on unclassifiable divergence. Durable replacement recovery is outside the initial scope.
4. **Error reporting:** daemon outcomes preserve specific stale/precondition/partial/uncertain and recovery diagnostics. ASP preserves firewall outcome separately from metadata-reconciliation outcome and surfaces backend failures with actionable typed messages rather than collapsing them into a generic error.

These decisions are the target contract for the implementation phases above.
