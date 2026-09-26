# First-class rule groups implementation plan

This is a non-normative implementation plan for introducing **rule groups** as an ASP-owned logical and operational grouping of live UFW rules. Groups are deliberately distinct from tags: tags remain many-to-many organizational labels, while a rule can belong to at most one group and a group can drive operations over all of its member rules.

The plan is temporary. Once the implementation is approved, its durable contracts must be reconciled into the permanent architecture, protocol, security, development, and testing documentation and this file must be removed.

## Goals and invariants

The implementation must preserve the existing authority split:

- UFW remains authoritative for the live firewall rule set and ordering.
- ASP owns group definitions and semantic rule-to-group membership as presentation/operational metadata.
- The daemon must not receive or reason about ASP group IDs. Group-wide firewall mutations are expressed as generic signed operations over an exact authoritative firewall snapshot.
- A semantic rule identity can belong to zero or one group. Duplicate live occurrences of the same semantic rule therefore share one metadata/group association.
- Groups may exist without any member rules.
- Deleting an empty group is an ASP-only operation and does not require a signed firewall intent.
- Deleting a non-empty group is a composed workflow: authorize and execute deletion of the live member occurrences first, reconcile ASP metadata from the resulting authoritative snapshot, then delete the group only when it is actually empty.
- Partial or uncertain firewall mutation outcomes must never cause ASP to pretend that the group or its surviving memberships were deleted.
- Group names are mutable presentation data. Filters and relationships use stable public IDs rather than names.

## Existing implementation inventory

### ASP persistence and rule enrichment

`RuleMetadataEntry` is the natural ownership point for group membership. It already provides the one-per-semantic-rule ASP record keyed by stable `RuleId`, stores notes, and owns the many-to-many tag relationship. `RuleInventoryService` joins those records onto the daemon-owned rule snapshot.

The group model should therefore use:

- a standalone `RuleGroupEntry` with numeric database key, public ID, name, and optional comment;
- a nullable group foreign key/navigation on `RuleMetadataEntry`;
- a restrictive group foreign key rather than database cascade deletion.

A nullable FK structurally enforces zero-or-one group membership while still allowing empty groups. Database cascade deletion is intentionally inappropriate: deleting an ASP row must never make live firewall rules disappear conceptually or silently erase their membership before the firewall operation succeeds.

The existing tag repository/service/controller flow is the closest CRUD reference. `RuleMetadataRepository : DatabaseService<ApplicationDbContext>` and the WKG shared transaction scope remain the persistence boundary for metadata/group updates.

### Browser metadata editing and rule creation

`RuleMetadataEditor` is shared by live rule metadata editing and the add-rule flow. It already supports lazy tag creation from autocomplete suggestions, so it is also the correct shared boundary for a single-select group autocomplete:

- choose an existing group by stable public ID;
- clear membership;
- enter a new group name and accept it to create a group with no comment, then select it.

The resulting metadata contract should carry one nullable group public ID alongside notes and tag IDs. No second group-specific editing path should be introduced for rule authoring.

### Filtering and search

The browser query pipeline is already modular: filter models are evaluated independently and surfaced through filter definitions/editors/evidence. Tag filtering is the closest reference.

Groups should add a dedicated filter keyed by group public ID. Text search should include the effective group name and comment as searchable evidence. Renaming a group must not invalidate an existing structured group filter.

### Rule presentation and reusable UI

The desktop rule details pane is currently organized around `RuleMetadataDetails`: metadata/known-host information, canonical command, tags, notes, and actions. Group membership belongs in this presentation surface above the canonical command, visually near the **Rule metadata** heading, matching the requested layout without making the command or group into a separate table row.

The main firewall row/card components are strongly specialized around firewall semantics, drag/reorder behavior, and match evidence. The group manager should reuse focused primitives and styling conventions rather than prematurely generalizing the entire rule-row hierarchy. Shared expandable-row/action-menu/responsive inventory primitives should be extracted only where both pages genuinely share behavior rather than appearance alone.

### Single-rule deletion

The existing single delete flow signs one exact semantic rule mutation. The daemon re-snapshots immediately before execution, requires one unambiguous matching occurrence, deletes the current UFW display number, and verifies the resulting state.

The optional **delete group as well** checkbox does not belong in that signed firewall payload. After a successful single-rule deletion and metadata reconciliation, the browser may issue an ASP-only empty-group delete. If a concurrent membership appears, the group delete must fail as in-use while the already-authorized firewall deletion remains successful.

### Reordering as the batch-mutation reference

Reordering already provides the important machinery for a stateful multi-step firewall mutation:

- exact versioned snapshot fingerprints;
- occurrence-based addressing of listed rules;
- signed intent verification and nonce consumption;
- a daemon-wide execution gate;
- iterative re-snapshotting and validation;
- explicit completed, partial, stale, and uncertain outcomes.

Batch deletion should reuse those concepts, but not reorder recovery semantics. Recreating a rule after a partially completed delete would itself be a new firewall mutation that the administrator did not authorize.

## Data and API model

### Group persistence

The initial group entity should contain:

```text
Id          numeric primary key, database-internal
PublicId    stable public UUID
Name        required, case-insensitively unique, bounded consistently with tag names
Comment     optional bounded text
```

`RuleMetadataEntry.GroupId` is nullable and points to the group with restrictive delete behavior. `RuleMetadataValues` and the corresponding REST models gain one nullable group public ID.

Group APIs should support:

- list groups;
- get one group when useful to the manager;
- create group;
- update name/comment;
- delete group only when it has no metadata members;
- expose the group's semantic member `RuleId` values for management/reconciliation UI.

The rule inventory should expose only the effective group summary required for normal rule presentation/filtering, not the group's entire membership collection.

Group names should follow the same case-insensitive uniqueness model as tags unless implementation constraints discovered in Phase 1 justify a different explicit contract.

### Orphaned metadata

A group can temporarily reference metadata for a semantic rule no longer present in the authoritative UFW snapshot, for example after out-of-band firewall changes. The group-management details view should not silently hide this condition: it can join member `RuleId`s against the current rule inventory and identify unmatched memberships as stale/orphaned metadata.

The existing metadata reconciliation operation remains responsible for removing unmatched rule metadata. Reconciliation removes the stale membership but preserves the group itself, including when the group becomes empty.

## Signed batch-delete protocol

Group-wide deletion needs one authorization event covering the exact collection the administrator reviewed. The daemon-facing operation should therefore be a generic batch rule deletion rather than a group mutation.

### Signed intent

Introduce a new signed operation, provisionally `rules.delete-batch`, whose payload contains:

```text
baselineFingerprint    exact versioned fingerprint of the authoritative listed-rule snapshot
occurrenceIds[]        non-empty, unique snapshot-local occurrence IDs
```

Occurrence IDs are the same kind of positional occurrence identity used by reordering. The signed baseline fingerprint already commits to each listed occurrence's active state, position/display number, parsed/raw representation, semantic ID, and specification, so the payload should not duplicate independent rule specifications unless implementation proves that necessary for validation.

Adding a new accepted signed operation changes the protocol/security contract. Because client and server enforce exact protocol-version compatibility, the signed-intent context/version must be advanced explicitly rather than silently widening the current version.

### Daemon execution

Execution runs under the same mutation gate as other firewall writes. The executor should:

1. acquire a fresh listed-rule snapshot and require an exact baseline fingerprint match before the first mutation;
2. validate that all requested occurrences are unique, in range, and addressable;
3. derive the exact expected remaining occurrence sequence after each requested deletion;
4. before every subprocess mutation, obtain a fresh snapshot and require it to match the expected state after all previously confirmed deletions;
5. resolve the next baseline occurrence to its current UFW display number and delete it;
6. immediately re-snapshot and require the expected post-delete state before advancing;
7. return the final authoritative snapshot whenever it is known.

Deleting targets in descending baseline order is a useful implementation simplification because UFW display numbers shift downward, but correctness must come from fresh reconciliation rather than arithmetic assumptions about display numbers.

The response should distinguish at least:

- completed;
- stale baseline/precondition failure before mutation;
- partially completed with a known authoritative state;
- state uncertain after a subprocess/observation failure.

Per-target operation reports and the still-pending targets should make partial outcomes diagnosable and allow the browser to explain what happened. There is intentionally no automatic reinsertion/recovery journal for confirmed deletions.

### ASP metadata reconciliation after batch deletion

Metadata cleanup must be driven by the returned authoritative final state, not by the requested target list alone. In particular, duplicate live occurrences can share one semantic `RuleId`; deleting one occurrence must not delete its metadata while another occurrence remains.

For a known final snapshot, ASP may remove metadata only for semantic IDs whose targeted occurrences were confirmed deleted **and** whose semantic ID is absent from that final snapshot. Partial results therefore clean up only what is conclusively gone. An uncertain final firewall state must not trigger speculative metadata deletion.

## Browser deletion workflows

### Delete group from the group manager

Deleting a group with live members is a composed operation:

1. refresh authoritative rule/group state;
2. derive the current live occurrences belonging to the group;
3. show a destructive confirmation containing the exact rules to be removed;
4. collect the signing key and sign one batch-delete intent over the exact baseline snapshot/occurrences;
5. submit the signed mutation and present its structured outcome;
6. refresh/reconcile authoritative state;
7. delete the ASP group only if the firewall operation completed to a known state and the group is now empty.

If new membership appears concurrently before step 7, empty-only group deletion returns an in-use conflict and the group remains. Partial/uncertain batch outcomes also leave the group in place.

An already-empty group skips the signed firewall flow and is deleted directly from ASP.

### Delete the last rule of a group

The ordinary single-rule delete dialog should detect when the selected semantic rule is currently the group's final live member and offer an unchecked **delete group <name> as well** option.

The existing single-rule signed delete remains unchanged. When selected, the follow-up group delete runs only after the rule deletion and normal metadata reconciliation succeed. The server still enforces empty-only deletion, so stale client state cannot delete a group that acquired another member concurrently.

## Group-management UI

The metadata landing page gains a **Manage groups** entry that navigates to a dedicated manager rather than opening an oversized combined metadata dialog.

The group manager supports:

- create with name and optional comment;
- edit name/comment;
- delete, using the empty or signed batch workflow described above;
- responsive rows/cards showing name, comment, and an overflow action menu;
- expandable details in the same visual language as the firewall table;
- member-rule presentation inside the expanded details, reusing existing rule presentation primitives where they are appropriately scoped;
- explicit presentation of stale/orphaned metadata memberships when a stored member `RuleId` is absent from the current rule snapshot.

This phase should identify focused reusable components rather than forcing the specialized firewall workspace and generic ASP entity manager into one inheritance/component hierarchy.

## Implementation phases and review gates

All work is based on the parent `feature/rule-groups` branch. Each phase is developed on its own sub-branch, reviewed, and merged locally into the parent only after approval. A later phase starts from the newly approved parent state.

### Phase 0: inventory and implementation plan

Branch: `phase/rule-groups-0-plan`

- inventory the persistence, metadata, tag, filtering, rule-presentation, single-delete, signed-intent, and reorder paths;
- record invariants and cross-layer ownership boundaries;
- define the phased implementation and validation strategy in this document.

**Gate:** review this plan before any production implementation begins.

### Phase 1: group persistence and ASP API

Branch: `phase/rule-groups-1-persistence-api`

- add the EF group entity, mapping, migration, nullable metadata FK, and transaction-aware repository/service layer;
- add group REST models/controller endpoints and member semantic-ID inventory;
- extend rule metadata read/write contracts with nullable group public ID and effective group summary;
- update serialization/source-generation/DI registration as required;
- add focused database/service/controller integration tests.

Acceptance includes empty groups, case-insensitive name uniqueness, group-only metadata, at-most-one membership, transactional rejection of unknown groups, in-use group-delete conflicts, and preservation of groups during orphan metadata reconciliation.

**Gate:** review persistence/API semantics and schema before browser coupling.

### Phase 2: rule integration, filtering, and metadata UX

Branch: `phase/rule-groups-2-rule-integration`

- add client group models/catalog/API access;
- extend the shared metadata editor with nullable single-group selection and lazy name-only creation;
- persist group choice from both live metadata editing and add-rule flows;
- project group data into rule snapshots/presenters;
- place group membership above the canonical command in desktop/mobile details as appropriate;
- add modular structured group filtering and group search evidence;
- include group name/comment in text search;
- reconcile structured filters by stable public ID across catalog refreshes;
- extract tag/group shared abstractions only where actual duplicate behavior justifies one.

Acceptance includes rename-safe filters, clear membership, lazy creation, duplicate semantic rules sharing membership, and dark/light responsive visual review.

**Gate:** functional and visual review of ordinary group usage before the management surface.

### Phase 3: dedicated group-management UI

Branch: `phase/rule-groups-3-management-ui`

- add the metadata landing-page navigation entry and `/metadata/groups` manager;
- implement create/edit and empty-group deletion;
- add responsive rows/cards, overflow actions, and expandable member details;
- resolve member `RuleId`s against current rule inventory and surface stale memberships explicitly;
- extract reusable expansion/action/list primitives only where doing so improves both existing and new callers.

Non-empty destructive group deletion can be displayed as unavailable/pending until the signed batch operation exists; do not fake it with sequential unsigned/singly signed deletes.

**Gate:** visual/interaction review of group management before protocol work.

### Phase 4: signed batch-delete protocol and daemon execution

Branch: `phase/rule-groups-4-batch-delete-protocol`

- advance the signed protocol context/version and add the generic batch-delete operation/payload/canonicalization/source-generation support;
- implement browser signing helpers and ASP forwarding contracts without introducing group semantics into the daemon protocol;
- add daemon authorization/nonce handling and iterative batch-delete executor under the shared firewall mutation gate;
- return structured per-target, final-state, partial, stale, and uncertain outcomes;
- reconcile ASP rule metadata only from known authoritative final snapshots;
- add exhaustive canonicalization/signature/replay/stale-state/duplicate/failure integration tests modelled on mutation and reorder coverage.

**Gate:** security/protocol review before wiring the operation to group deletion UX.

### Phase 5: destructive group workflows

Branch: `phase/rule-groups-5-delete-workflows`

- implement non-empty group-delete confirmation with complete member preview and client-side signing;
- execute batch delete, present partial/uncertain outcomes, refresh state, and perform final empty-only ASP group deletion;
- extend ordinary single-rule deletion with the optional last-member **delete group as well** checkbox;
- cover concurrent membership changes, duplicate occurrences, partial daemon outcomes, and metadata-cleanup races.

**Gate:** end-to-end destructive-operation and UX review.

### Phase 6: steady-state documentation reconciliation

Branch: `phase/rule-groups-6-docs`

Apply `technical-documentation.skill` against the completed implementation rather than documenting this plan verbatim. Reconcile at least the permanent documents whose contracts actually changed, expected to include:

- `docs/architecture/browser-application.md`;
- `docs/architecture/firewall-model.md`;
- `docs/architecture/security.md`;
- `docs/protocols/application-protocol.md`;
- `docs/protocols/signed-intent.md`;
- `docs/protocols/README.md`;
- relevant testing/development documentation where new extension/testing contracts materially belong.

Describe the steady-state ownership model, group lifecycle, batch-delete authorization/reconciliation, filtering, and failure semantics at architecture/protocol level. Preserve accurate existing prose and avoid class inventories or implementation-history narration.

Delete this implementation plan after its durable contracts are represented in permanent documentation, as required by `docs/internal/README.md`.

**Gate:** documentation coherence review plus final full regression validation.

## Cross-phase validation expectations

Each production phase should run the strongest focused tests for the touched layers and `git diff --check`. Before each review gate, build the solution offline with the supplied SDK/package cache. Phases that cross security/protocol or EF boundaries should include integration coverage rather than relying only on mocked unit tests.

The final feature branch must pass the complete offline solution build and test suite. Regression coverage should specifically include:

- one-group-per-semantic-rule enforcement and duplicate live occurrences;
- empty groups and group rename/comment changes;
- lazy creation and concurrent duplicate-name attempts;
- structured group filter rename stability and text-search evidence;
- stale/orphaned metadata reconciliation;
- exact signed batch payload canonicalization and protocol-version enforcement;
- replay rejection, stale baseline rejection, mutation-gate serialization, and iterative snapshot drift detection;
- partial/uncertain batch deletes without speculative metadata/group deletion;
- single-rule last-member deletion with and without group cleanup;
- concurrent reassignment preventing final group deletion;
- IPv4/IPv6 and inactive/listed-rule cases already represented by the existing authoritative snapshot model.

## Non-goals for the first implementation

Keep the initial feature focused. In particular:

- no group colors or visual theming beyond the normal application design system;
- no nested groups or many-to-many group membership;
- no daemon persistence/knowledge of groups;
- no group-specific UFW syntax or comments;
- no automatic deletion of empty groups unless the user explicitly requests it in the supported delete workflows;
- no generic bulk mutation framework beyond abstractions that batch deletion demonstrably needs;
- no speculative generalization of the firewall rule table into a universal entity-table framework.
