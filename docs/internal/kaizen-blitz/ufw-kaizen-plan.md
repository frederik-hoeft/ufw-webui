# UFW Management Interface - Overall Kaizen Blitz Plan

## Purpose

This document combines the three project backlogs into one dependency-driven execution plan:

- `Ufw.Systemd` (daemon)
- `Ufw.Web` (ASP aggregation/enrichment layer)
- `Ufw.Web.Client` (Blazor client)

The goal is not to execute each backlog in its original priority order. All tracked debt is intended to be resolved, so the plan optimizes for **dependency order and minimal temporary work**: establish broad primitives and contracts first, then perform narrower cleanup against the final architecture. The preferred project order remains **daemon -> Ufw.Web -> client**, with narrowly scoped cross-project bridge commits where a shared contract must continue to compile.

The three source backlogs remain the authority for detailed evidence, affected files, and item-specific acceptance criteria. This plan owns sequencing, overlap, cross-project dependencies, and final traceability.

## Planning principles

1. **Broader fixes subsume local fixes.** Do not add a temporary local abstraction when a scheduled foundational item will delete or replace it.
2. **Stabilize producers before consumers.** Daemon wire/protocol/transport behavior is stabilized before Web gateways; Web HTTP/shared-model contracts are stabilized before client workflow extraction.
3. **Shared contracts are bridge work, not a fourth project phase.** Changes in `Ufw.Shared` or `Ufw.Web.Model` should be made at the phase boundary where their semantics become stable, with only the minimum downstream compile fixes until that downstream project's phase.
4. **Do not generalize across process boundaries merely because concepts look similar.** Snapshot identity, error classification, and metadata projection have related semantics across daemon/Web/client, but each layer keeps the abstraction appropriate to its responsibility.
5. **Refactor around final call shapes.** Decompose large classes only after the shared primitives they should consume exist.
6. **Every source backlog item remains independently checkable.** An item may be implemented as part of another item, but it is never silently dropped.

## Global dependency map

| Concern | Daemon foundation | Web consumer/bridge | Client consumer | Required order |
|---|---|---|---|---|
| Signed mutation / intent protocol | SYS KZ-014, KZ-008, KZ-015, KZ-027 | WEB KZ-02, KZ-03, KZ-17 | CLIENT KZ-04 | Daemon verifier + mutation choreography -> Web gateway/protocol interpretation -> browser signing/context cleanup |
| Authoritative firewall state / identity / ordering | SYS KZ-004, KZ-005, KZ-016, KZ-026, KZ-029 | WEB KZ-08 | CLIENT KZ-03, KZ-05, KZ-17 | Stabilize daemon snapshot/mutation semantics first; then align server/client identity semantics without forcing one abstraction everywhere |
| Transport and configuration | SYS KZ-009, KZ-011, KZ-018, KZ-019, KZ-025 | WEB KZ-02, KZ-04 | Mostly transparent to browser | Daemon configuration/transport selection -> Web daemon gateway/wiring -> Startup cleanup |
| Error/diagnostic contract | SYS KZ-012, KZ-020 | WEB KZ-22, KZ-02, KZ-15, KZ-09 | CLIENT KZ-11, KZ-19 | Daemon remote-error policy -> Web application/gateway error model -> public ProblemDetails -> client mapping/workflows |
| REST validation and shared limits | Shared validator/model work; daemon may consume shared validators | WEB KZ-05, KZ-14 | CLIENT KZ-08, KZ-12 | Establish stable shared limits/validation identities before client authoring/localization cleanup |
| Shared management-domain models / metadata | N/A | WEB KZ-01, KZ-06, KZ-23 | CLIENT KZ-06, KZ-08, KZ-17 | Web defines stable persistence-vs-domain-vs-transport roles -> client consumes the shared domain model directly |
| Network-interface lifecycle | Daemon remains source of interface presence | WEB KZ-13 + KZ-01 interface slice | CLIENT KZ-10, KZ-19, KZ-24 | W2.4 fixes retained-metadata/presence semantics and cleanup contracts before client inventory/reference-data/cleanup state is refactored |
| Firewall semantic-domain algebra | PR #40 / `Ufw.Shared.Domain` is the shared foundation | Server should not reverse the Firewall -> Domain dependency | CLIENT KZ-02, then KZ-16 | PR #40 is merged; adapt the client to the shared semantic domain before filter micro-cleanup |

### Important overlap that should **not** become one shared abstraction

- WEB KZ-08 and CLIENT KZ-03 both concern rule identity, but the server needs a set of live semantic IDs while the client needs snapshot-local **occurrence** identity and positions. Align the meaning of `RuleId`; do not collapse these into one inappropriate helper.
- WEB KZ-06 and CLIENT KZ-06 both concern metadata, but the server item is relational tag/group persistence while the client item is protocol-to-domain validation/mapping. They share models, not implementation.
- SYS KZ-005 and WEB KZ-22 both use the word “result/error”, but one classifies UFW process execution and the other classifies application/persistence mutation failures. Keep their error domains distinct and translate only at process boundaries.
- Daemon reorder/replacement state machines remain operation-specific even after shared process/snapshot primitives are extracted.

## Work that should deliberately **not** be done as standalone patches

| Item | Avoid | Planned treatment |
|---|---|---|
| WEB KZ-14 | Add another temporary `KnownHostMutationOutcome` branch | Resolve during WEB KZ-05 request validation/mapping; keep a regression test for the original diagnostic bug |
| WEB KZ-15 | Patch the current daemon mapper and later replace it | Resolve while WEB KZ-02/KZ-09 centralize daemon/error mapping |
| WEB KZ-17 | Create a route-constants class next to direct `IUfwClient` usage | Let routes become private implementation details of WEB KZ-02 gateways |
| WEB KZ-21 | Mechanically change every `ToArrayAsync` today | Run after WEB KZ-01; many materializations may disappear, then change only surviving call sites |
| WEB KZ-07 | Deduplicate current group/tag service/controller plumbing | Wait for WEB KZ-22/KZ-23; then only remaining genuine duplication is abstracted |
| WEB KZ-04 | Decompose `Startup` against today's registrations | Do after DAL/gateway/service topology stabilizes |
| SYS KZ-001 | Build bespoke nonce-file recovery mechanics | Establish SYS KZ-013 durable-file primitives first, then implement nonce consistency/compaction on them |
| SYS KZ-006/KZ-007 | Split executors around duplicated helpers | Establish SYS KZ-004/KZ-005/KZ-016 first, then decompose around those primitives |
| SYS KZ-003 | Refactor timing around the current mutable middleware pipeline | Settle SYS KZ-021 composition first, then make request timing exception-safe once |
| CLIENT KZ-01 | Extract workflows that encode today's API DTO/error quirks | Wait for the Web API/shared-model checkpoint and client C1 protocol mappers |
| CLIENT KZ-11 | Generalize current exception handling before server errors stabilize | Consume final WEB KZ-09 ProblemDetails/error semantics first |
| CLIENT KZ-16 | Abstract the current filter evaluators/editors | First replace duplicated CIDR/port semantics via CLIENT KZ-02, then deduplicate the smaller final code |
| CLIENT KZ-13 | Consolidate styles around duplicated desktop/mobile structures | First perform CLIENT KZ-07 component convergence, then classify/extract styles |

## Execution plan

### Phase D - `Ufw.Systemd`

The daemon is the producer of the mutation, intent, snapshot, diagnostic, and transport semantics consumed by ASP. Complete its architectural changes before standardizing Web's daemon boundary.

#### D1 - Lifecycle, durability, transport, and API infrastructure

Recommended internal order:

1. **Configuration decision first:** SYS KZ-009 -> KZ-011/KZ-012/KZ-025. Decide startup-immutable vs reload semantics before touching consumers.
2. **Transport/security on the final configuration model:** SYS KZ-018 -> KZ-019. Official TCP support and transport selection are established here.
3. **Durable storage foundation:** SYS KZ-013 -> KZ-001. Apply the common durability primitive to nonce persistence/compaction instead of fixing nonce I/O twice.
4. **Network lifecycle:** SYS KZ-010 after configuration semantics are known.
5. **API pipeline:** SYS KZ-021 -> KZ-020 -> KZ-003. Compose middleware once, then fix endpoint invocation and request timing against that shape.
6. Leave pipe-specific KZ-002 until D4, after KZ-019 has settled final pipe ownership.

#### D2 - Authoritative-state and execution primitives

Recommended order:

1. SYS KZ-017 command execution/output parsing.
2. SYS KZ-004 explicit authoritative snapshot success/failure result.
3. SYS KZ-026 equality/comparer naming and SYS KZ-028 resilient rule-spec copying while core model semantics are in view.
4. SYS KZ-005 shared process/snapshot/diagnostic primitives.
5. SYS KZ-016 one insertion-placement resolver.
6. SYS KZ-027 execution-gate overload.
7. SYS KZ-029 explicit reorder cost semantics, before the reorder executor is split.

#### D3 - Intent/mutation workflow cleanup and hotspot decomposition

Recommended order:

1. SYS KZ-014 split stable intent-envelope verification from operation payload binding.
2. SYS KZ-015 keep key ownership/verification inside the key store, aligned with the configuration lifecycle.
3. SYS KZ-008 signed-mutation gate/safety/nonce choreography, built on KZ-014/KZ-027.
4. SYS KZ-006 decompose reorder using the final snapshot/process/placement/cost primitives.
5. SYS KZ-007 decompose replacement using the same final primitives.

#### D4 - Contained cleanup and daemon handoff

1. SYS KZ-002 pipe stream ownership/disposal against the final transport implementation.
2. SYS KZ-022 logging consolidation after configuration/API lifecycle changes.
3. SYS KZ-023 + KZ-024 parser cleanup as one parser-focused change.
4. Run the daemon-specific definition of done and freeze the **consumer-visible** contract for the Web phase: supported transports/configuration, intent canonicalization/version behavior, mutation response semantics, snapshot identity semantics, and remote diagnostic exposure.

### Bridge checkpoint S1 - daemon -> Web

Wave D is complete. S1 treats the consumer-visible daemon behavior finalized in D4 as frozen; daemon internals should not be reopened unless bridge work discovers a genuine shared-contract defect. The current standard Web deployment/client remains Unix-pipe based even though the daemon server also supports TCP.

**Status:** complete. S1 established the provider-side validation identity contract before structural Web work:

- firewall-rule validation failures now carry an open, stable string code alongside property and human-readable diagnostic text;
- current shared, daemon, and client-side rule-validation producers populate the shared codes, while application-v1 readers continue to accept legacy validation payloads without a code;
- IPC client propagation preserves the validation array and stable codes without additional translation;
- the current Web `ValidationProblemDetails` mapping and client localization behavior remain unchanged intentionally. WEB KZ-09/KZ-15 own the final public HTTP error contract, and CLIENT KZ-12 owns switching localization from English text to stable codes.

### Phase W - `Ufw.Web`

ASP is an aggregation/enrichment layer, so its biggest dependency is having a stable daemon producer and then establishing one coherent DAL/shared-model/error boundary before migrating individual features.

#### W1 - Boundary architecture and shared-contract foundation

Treat these as one architecture package rather than independent tickets:

1. **WEB KZ-01 + KZ-05 + KZ-22 + KZ-23:** finalize persistence entities vs shared domain/read models vs HTTP envelopes; request DTO validation/mapping; common mutation errors; and the rule that application coordinators must earn their existence.
2. **WEB KZ-13 decision before interface migration:** transiently missing interfaces retain application-owned metadata in a soft-deleted/non-present state; reappearance revives the same row, while permanent purge is explicit cleanup.
3. **WEB KZ-02 + KZ-17:** introduce the final daemon gateway against the now-stable daemon transport/protocol; endpoint paths disappear inside it.
4. **WEB KZ-03:** move signed daemon interpretation behind that gateway and pass domain reconciliation facts to metadata persistence.
5. **WEB KZ-14 is resolved here through KZ-05**, not by adding another temporary outcome type.
6. **WEB KZ-12:** remove genuinely obsolete signing/stale code early, but retain the existing reusable validation attributes for KZ-05 DTO validation rather than deleting and recreating them.

Implementation should then migrate one domain slice at a time into `Data/Access/<domain>`, keeping the solution buildable. Pure management-domain/read models move to `Ufw.Shared.Management`; versioned request/response DTOs remain in `Ufw.Web.Model` and may embed those shared types. Make only the minimum client compile edits in the same commit; defer client-side architectural cleanup to Phase C.

W1.1 uses rule groups as the first vertical boundary pilot: group read models/limits move to `Ufw.Shared.Management`, request-shape validation and normalization move to the REST boundary, and the controller talks directly to the domain-sliced DAL. The second review cycle applies the same boundary to rule tags, establishes reusable `DataMutationResult`/`DataMutationError` propagation for common not-found/unique/reference failures, and removes the obsolete KZ-12 RSA signing provider while retaining the reusable validation attributes for KZ-05 DTO validation. The third review cycle applies the boundary to rule templates: the template read model and cross-layer limits live in `Ufw.Shared.Management.Rules`, request-shape validation is declarative on the V1 DTO, semantic firewall-rule validation remains a pure domain check during request mapping, the ceremonial template service disappears, and payload-bearing missing-tag/group errors demonstrate the typed `DataMutationError` union. Shared metadata limits are centralized separately from template-only limits because live-rule metadata consumes the same notes/tag-count invariants. The broader KZ-01/KZ-05/KZ-22/KZ-23 checklist items remain open until the remaining Web slices have migrated to the same rules.

W1.2.1 establishes feature-oriented daemon gateways without changing HTTP failure policy: rule reads and signed mutations use `IRuleDaemonGateway`, network-interface response validation lives in `INetworkInterfaceDaemonGateway`, and status/intent use narrow dedicated gateways. `IUfwClient` and the unsigned read/probe route strings are now private gateway implementation details; signed mutation routes remain owned by their shared request-message contracts. WEB KZ-17 is therefore complete.

W1.2.2 completes WEB KZ-02. `Ufw.Ipc.Client` now exposes non-throwing `TrySendAsync` operations that return `UfwIpcResult`/`UfwIpcResult<T>` for daemon-declared application failures; the legacy/assertive `SendAsync` surface is implemented on top and throws only when success is explicitly asserted. Web gateways map those IPC results into `DaemonResult`/`DaemonResult<T>` without exception-driven control flow: ordinary callers use `Result`/`EnsureSuccess()` and rely on the centralized MVC daemon exception filter, while workflows that intentionally reinterpret daemon failures can use `TryGetResult`. The network-interface reconciliation path is the first such consumer and deliberately classifies daemon application failure as upstream unavailability. Invalid IPC/daemon response shape is translated to a daemon-specific exception at the gateway boundary, so the global filter never treats unrelated application `InvalidDataException`s as daemon failures. Existing operation-specific cleanup/reconciliation catches remain local to their workflows. The filter preserves the existing daemon-to-HTTP mapping for now; WEB KZ-09/KZ-15 still own the final public ProblemDetails contract and validation-detail preservation.

W1.3 completes WEB KZ-03. Signed rule-replacement interpretation now belongs to `IRuleDaemonGateway`: a completed and coherent daemon result yields explicit `RuleReplacementReconciliationFacts`, a non-completed firewall outcome requires no metadata reconciliation, and a completed-but-inconsistent request/response pair yields reconciliation preparation failure while preserving the authoritative firewall report. `RuleMetadataService` consumes only those domain facts and owns persistence reconciliation, so metadata storage no longer parses signed payload JSON or understands daemon replacement response structure.

#### W2 - Vertical DAL/workflow migrations and public error contract

W2.1 starts with the remaining rule-metadata persistence boundary. `RuleMetadataItem` now lives with the other pure rule-management read models in `Ufw.Shared.Management.Rules`; metadata EF access is confined to `Data/Access/Rules/Metadata` and projects reads directly into that shared model. Metadata and templates now share one persistence-level tag/group dependency resolver plus one tag-relation diff synchronizer, closing WEB KZ-06 without a generic repository hierarchy. Metadata persistence also consumes the common typed `DataMutationError` vocabulary through payload-bearing `DataMutationResult<T>`, while the higher-level metadata workflow remains responsible for daemon liveness and best-effort post-firewall reconciliation.

W2.2 centralizes Web-side live semantic rule identity through `LiveRuleIdentitySet`, which derives one ordinal, deterministically enumerable set from authoritative daemon snapshots for inventory, metadata liveness/cleanup, and replacement reconciliation. Metadata bulk cleanup now uses set-based `ExecuteDeleteAsync` operations in the final DAL, with regression coverage for affected-row counts and database-cascaded metadata-tag relations. This closes WEB KZ-08 and KZ-18 without introducing a cross-process snapshot abstraction.

W2.3 migrates known hosts onto the final shared-domain/DAL boundary. `KnownHostInventoryItem`, `KnownHostAddressSource`, and cross-layer limits now live under `Ufw.Shared.Management.KnownHosts`; EF access is confined to `Data/Access/KnownHosts`; `KnownHostService` remains the DNS/reconciliation coordinator; and successful writes perform the refreshed inventory read only after the DAL transaction commits. Raw-value DTO validation now owns known-host name/comment shape, closing WEB KZ-14 without adding another mutation outcome. KZ-24 remains open for the API-wide request-validation audit.

W2.4 migrates network interfaces onto the final shared-domain/DAL boundary and implements the KZ-13 retained-metadata lifecycle. `NetworkInterfaceInventoryItem`, inventory snapshots, and cross-layer limits live under `Ufw.Shared.Management.NetworkInterfaces`; EF access is confined to `Data/Access/NetworkInterfaces`; reconciliation marks missing interfaces non-present instead of deleting their application metadata; and reappearance revives the existing row/identity. Normal inventory remains present-only. Dedicated stale-read and cleanup contracts expose retained metadata for the deferred client cleanup flow, and permanent cleanup revalidates daemon presence before deleting selected rows that are still absent.

W2.5 finalizes the Auth persistence/workflow boundary. `AuthenticationFlowService` owns each auth use-case transaction and passes the exact transaction `ApplicationDbContext` into `RefreshTokenDataAccess`; refresh-token persistence therefore no longer relies on nested Wkg transaction joining. Refresh-token contracts expose only user IDs/token facts rather than `IdentityUser`, and password-change validation no longer leaks `IdentityResult` into the controller. Refresh-cookie security options and Identity password-error translation are centralized, closing WEB KZ-10 and KZ-16 while preserving the current HTTP behavior for the later KZ-09 error-contract pass.

W2.6 completes the API-wide REST DTO validation audit. Rule-metadata update/cleanup shape is now declared on the V1 contracts, the dedicated metadata normalizer and controller/service duplicate guards are removed, known-host literal/DNS configuration is validated at the transport boundary, and group/tag/template validation now measures raw transport values before any request mapping normalization. The old Web-only IPv4/port validation attributes are removed after the audit confirmed they have no correct DTO consumer: firewall address/port semantics belong to the shared `RuleSpecificationValidator`, not a partial REST duplicate. Remaining inline request checks are protocol/domain semantics rather than transport shape. This completes WEB KZ-05/KZ-24 and leaves one settled MVC validation path for KZ-09.

Migrate slices against the W1 rules instead of doing horizontal repository rewrites:

1. **Rules groups/tags/templates/metadata:** WEB KZ-06, KZ-07, KZ-08, KZ-18. KZ-07 follows KZ-22/KZ-23; KZ-06 is implemented directly in the final DAL; KZ-18 becomes a set-based final-DAL optimization.
2. **Known hosts:** completed in W2.3: final DAL/shared-domain migration plus WEB KZ-14 regression coverage through raw request validation.
3. **Network interfaces:** completed in W2.4: final DAL/shared-domain migration plus retained non-present metadata, present-only normal inventory, and race-safe stale cleanup contracts for deferred CLIENT KZ-24.
4. **Auth:** completed in W2.5: WEB KZ-10 and KZ-16 moved refresh-token EF access behind the Auth DAL, made the flow coordinator the explicit transaction owner, centralized cookie policy, and removed Identity result/error-code leakage from the controller contract.
5. **REST DTO validation audit:** completed in W2.6: WEB KZ-24 audited the full V1 request surface, removed duplicate transport-shape checks from application logic, and closed the API-wide completeness aspect of KZ-05.
6. **Public errors:** WEB KZ-09 after KZ-22, KZ-24, and the daemon gateway are stable. Fold WEB KZ-15 into this work and preserve all same-property daemon validation messages.
7. **Materialization audit:** WEB KZ-21 last within W2, after query shapes are final. Remove obsolete calls before converting surviving array materializations.

#### W3 - Composition and final server cleanup

1. WEB KZ-04 decompose Startup against the final registrations/lifecycle.
2. WEB KZ-11 internalize accidental public contracts now that actual boundaries are known.
3. WEB KZ-19 close the destructive-migration policy/documentation item without rewriting applied history.
4. WEB KZ-20 only if the resulting MVC convention remains more transparent than repeated attributes.

### Bridge checkpoint S2 - Web -> client

The client phase starts only after these are stable:

- shared management-domain model names/shapes used by both server and client;
- shared limits and stable validation identities;
- REST request/response contracts and `ProblemDetails` conventions;
- daemon mutation behavior as exposed through the Web gateway;
- network-interface retention semantics;
- rule metadata/tag/group/template response semantics.

Generate/inspect OpenAPI and run Web integration tests here. This checkpoint is the point after which client workflows can be extracted without immediately chasing server contract churn.

### Phase C - `Ufw.Web.Client`

#### C1 - Semantic and protocol foundations

1. PR #40 is merged; CLIENT KZ-02 adopts the resulting `Ufw.Shared.Domain` network/port semantics.
2. CLIENT KZ-03 snapshot occurrence index and CLIENT KZ-05 validated permutation invariant.
3. CLIENT KZ-04 signing/context consolidation, now consuming the stabilized daemon/Web intent contract.
4. CLIENT KZ-06 metadata protocol mapper + KZ-17 response-to-snapshot factory against the stabilized shared domain models.
5. CLIENT KZ-11 client error-mapping ergonomics against final WEB KZ-09 errors.
6. CLIENT KZ-12 switch localization to the stable shared validation identities established at S1/S2.
7. CLIENT KZ-15 settle catalog-state/`Version` semantics before metadata authoring is extracted.
8. CLIENT KZ-16 filter micro-clone cleanup only after KZ-02 has deleted the duplicated semantic algorithms.

#### C2 - Feature/workflow extraction

1. CLIENT KZ-01 move rule-page workflows into focused Features services using the C1 snapshot/protocol/error primitives.
2. CLIENT KZ-18 split group-deletion planning from execution and have workflow code consume the planner.
3. CLIENT KZ-08 move metadata-authoring behavior out of UI, consuming server-provided/shared limits and final catalog semantics.
4. CLIENT KZ-09 introduce explicit create-rule workflow state after the workflow has moved out of Razor.
5. CLIENT KZ-19 make rule-editor reference-data failures symmetric against the final server error behavior.
6. CLIENT KZ-10 factor known-host/interface inventory mechanics after Web's interface-retention semantics are final.

#### C3 - UI/component/style convergence

1. CLIENT KZ-07 converge desktop/mobile rule behavior and fragments after application workflows have left the components/pages.
2. CLIENT KZ-13 classify/extract styles against the final component structure.
3. CLIENT KZ-14 consolidate dialog options/confirmation presentation after workflow responsibility has been removed from dialogs.

#### C4 - Transport/public-surface/opportunistic cleanup

1. CLIENT KZ-20 + KZ-23 together: HTTP registration/resource mechanics and consistent URI building on the final API surface.
2. CLIENT KZ-21 public-surface audit after final Features/Api interfaces are known.
3. CLIENT KZ-22 last-mile presentational clones after all structural UI changes.

## Integration checkpoints

### Checkpoint D - daemon complete

**Status:** complete. The D4 definition-of-done audit revalidated all 29 daemon backlog items against the final tree. The phase gate passes a source-less offline restore, a zero-warning solution build, all 1,420 managed tests, and the `linux-x64` NativeAOT publish.

- Daemon backlog is fully green.
- Mutation state machines use one authoritative snapshot/process/placement foundation.
- Intent verification/canonicalization and remote error exposure are stable.
- Configuration lifecycle and transport selection are explicit.
- Web-facing shared protocol changes are documented before Web refactoring begins.

### Checkpoint W - Web complete

- No direct EF access outside the DAL except composition/migrations.
- Shared domain/read models are stable and separate from persistence entities/request DTOs.
- Daemon access is behind gateways; signed protocol parsing is not mixed with DB reconciliation.
- Public errors use one ProblemDetails policy and preserve daemon validation detail.
- Server-side query command counts and mutation behavior are regression-tested.
- OpenAPI/shared models are stable enough for client workflow extraction.

### Checkpoint C - client complete

- Rule workflows are outside Razor and consume final feature/domain abstractions.
- No duplicate client CIDR/port algebra remains.
- Snapshot identity, signing, permutation validation, metadata mapping, and error mapping each have one implementation.
- Desktop/mobile components share behavior without erasing layout differences.
- API/URI/style/public-surface cleanup is complete.

### Final repository gate

- Build/test all affected projects together, not just per-project test suites.
- Run daemon -> Web integration paths for rule list/mutations, validation failures, cancellation/reconciliation, status/intent, and configured transport(s).
- Run Web DB integration tests including SQL-command-count bounds for representative list/mutation paths.
- Run client unit/component tests plus publish/Sass/scoped-CSS validation.
- Verify protocol canonical-byte/signature tests across all signed operations.
- Verify no source backlog item remains unchecked in the traceability appendix below.

## Cross-project decisions to record explicitly

These are architecture decisions, not implementation details, and should be written down when resolved:

1. **Daemon configuration lifecycle — resolved in D1:** configuration is startup-immutable; changes require daemon restart (SYS KZ-009).
2. **Daemon transport model — daemon side resolved in D1:** the daemon selects `pipe` or `tcp` at startup and applies the same TLS/mTLS policy to either transport. The shipped production topology and current Web IPC client remain pipe-based; WEB KZ-02/KZ-04 own any Web-side transport selection needed to make TCP end-to-end (SYS KZ-019 -> WEB KZ-02/KZ-04).
3. **Network-interface metadata retention:** whether transient absence preserves user-owned metadata (WEB KZ-13 -> CLIENT KZ-10/KZ-19).
4. **Shared management-domain boundary — resolved in W1:** pure DAL/read objects live under `Ufw.Shared.Management`; `Ufw.Web.Model` remains the versioned HTTP DTO layer and may embed those shared types. Existing `*Item` names may remain where they describe read-model elements; avoid clone DTOs and keep DTO construction out of the DAL (WEB KZ-01).
5. **Stable validation identity contract — resolved in S1:** firewall-rule validation uses an open string code plus property and human-readable diagnostic text. Current producers populate the code; application-v1 readers accept a missing code only for compatibility with older payloads. WEB KZ-09/KZ-15 and CLIENT KZ-12 consume this identity without redefining it.
6. **Destructive migration policy:** preflight/fail vs explicitly accepted truncation for future schema changes (WEB KZ-19).

## Holistic traceability checklist

The source IDs are prefixed here with `SYS`, `WEB`, and `CLIENT` because the Web and client backlogs both use `KZ-01`-style numbering.

### `Ufw.Systemd`

| Done | Source item | Planned wave | Finding | Sequencing note |
|---|---|---|---|---|
| [x] | SYS KZ-001 | D1 | Make nonce consumption persistence-consistent | Implement after KZ-013 so nonce durability uses the shared durable-file primitive rather than bespoke I/O that would immediately be rewritten. |
| [x] | SYS KZ-002 | D4 | Fix named-pipe stream leaks on setup/accept failure | Do after KZ-019 settles the transport composition so pipe ownership/disposal is fixed in the final pipe implementation. |
| [x] | SYS KZ-003 | D1 | Give request logging exception-safe timing/lifecycle behavior | Do with/after KZ-021 so request timing is adapted once to the final middleware composition. |
| [x] | SYS KZ-004 | D2 | Define one authoritative firewall snapshot result type | Foundation for KZ-005, KZ-006 and KZ-007; establish the authoritative snapshot success/failure shape before decomposing executors. |
| [x] | SYS KZ-005 | D2 | Extract common UFW mutation execution primitives | Build on KZ-004 and KZ-017; do before executor decomposition. |
| [x] | SYS KZ-006 | D3 | Decompose `FirewallReorderExecutor` | Do only after KZ-004/KZ-005/KZ-016/KZ-029 so the split is around final shared primitives. |
| [x] | SYS KZ-007 | D3 | Decompose `FirewallRuleReplacementExecutor` | Do only after KZ-004/KZ-005/KZ-016; avoid extracting helpers that KZ-005 would replace. |
| [x] | SYS KZ-008 | D3 | Deduplicate signed mutation service choreography | Do after KZ-014 and KZ-027 so the common signed-mutation choreography consumes the final verifier/gate APIs. |
| [x] | SYS KZ-009 | D1 | Resolve configuration lifecycle ambiguity | First daemon architecture decision; it gates KZ-011, KZ-012, KZ-015, KZ-018, KZ-019 and KZ-025. |
| [x] | SYS KZ-010 | D1 | Supervise network worker capacity | Apply after configuration lifecycle is settled so worker-count/lifecycle semantics are not built on reload ambiguity. |
| [x] | SYS KZ-011 | D1 | Separate configuration shape validation from environment validation | Follow KZ-009; separate startup environment validation from immutable configuration shape. |
| [x] | SYS KZ-012 | D1 | Remove configuration drift and dead options; use safe diagnostics defaults | Follow KZ-009/KZ-011; its remote-diagnostics policy must be stable before Web standardizes daemon error handling. |
| [x] | SYS KZ-013 | D1 | Consolidate durable file persistence mechanics | Do before KZ-001 and before touching the other file-backed stores. |
| [x] | SYS KZ-014 | D3 | Split `IntentVerifier` into stable envelope verification and operation payload binding | Do before KZ-008 and before the client signing refactor; this is the server side of the shared intent/canonicalization contract. |
| [x] | SYS KZ-015 | D3 | Keep authorized-key ownership inside the key store | Do after KZ-009; align key lifetime/rotation behavior with the chosen configuration lifecycle and KZ-014 verifier split. |
| [x] | SYS KZ-016 | D2 | Unify insertion placement logic | Do before KZ-006/KZ-007 so insertion/reorder/recovery all consume one placement calculation. |
| [x] | SYS KZ-017 | D2 | Separate command execution from output parsing | Do before KZ-005; mutation execution primitives should be built on the final command/runner result shape. |
| [x] | SYS KZ-018 | D1 | Make transport security transport-neutral or name it pipe-specific | Do after KZ-009 and before KZ-019 so transport security consumes the final configuration model. |
| [x] | SYS KZ-019 | D1 | Remove or finish the dormant TCP server transport | Do after KZ-009/KZ-018. Its final transport-selection contract is a handoff input to Web KZ-02/KZ-04. |
| [x] | SYS KZ-020 | D1 | Deduplicate endpoint invocation/exception serialization | Do with KZ-021; deduplicate invocation/serialization against the final middleware/pipeline shape. |
| [x] | SYS KZ-021 | D1 | Simplify middleware composition | Do before KZ-003/KZ-020 so later API cleanup targets immutable composition. |
| [x] | SYS KZ-022 | D4 | Consolidate logging and remove bypasses | Do after KZ-012 and API/pipeline cleanup so logging has one final policy and no direct-console bypasses. |
| [x] | SYS KZ-023 | D4 | Reduce parser-to-grammar-name coupling | Contained parser cleanup after the mutation/snapshot foundation; preserve conservative unknown-row behavior. |
| [x] | SYS KZ-024 | D4 | Remove parser debug side effects and duplicate row-number parsing | Pair with KZ-023 while parser code is already being touched. |
| [x] | SYS KZ-025 | D1 | Reassess the configuration resource-provider abstraction | Resolve as part of KZ-009/KZ-011 configuration simplification rather than as a later abstraction rewrite. |
| [x] | SYS KZ-026 | D2 | Clarify normalized rule equality naming | Rename/clarify before shared snapshot/order helpers proliferate this comparer name. |
| [x] | SYS KZ-027 | D2 | Remove small API friction in the execution gate | Small prerequisite for cleaner KZ-008 orchestration. |
| [x] | SYS KZ-028 | D2 | Make rule specification copying resilient to model growth | Do while shared firewall model/execution primitives are being stabilized, before later model growth creates another manual-copy site. |
| [x] | SYS KZ-029 | D2 | Make reorder planner cost semantics explicit | Do before KZ-006 so the reordered executor is decomposed around an explicit cost policy. |

### `Ufw.Web`

| Done | Source item | Planned wave | Finding | Sequencing note |
|---|---|---|---|---|
| [x] | WEB KZ-14 | W2 | Fix known-host metadata validation being reported as an address error | Completed incrementally in W2.3 and finalized in W2.6: metadata plus literal/DNS request shape is rejected by DTO validation, so address/configuration failures are no longer overloaded mutation outcomes. |
| [ ] | WEB KZ-15 | W2 | Preserve multiple daemon validation errors for the same property | Subsumed by KZ-02/KZ-09 centralized daemon/error mapping; retain dedicated multi-error regression coverage. |
| [ ] | WEB KZ-01 | W1 | Establish a domain-sliced DAL and distinguish persistence entities, shared domain models, and API envelopes | Core Web architecture item. Co-design with KZ-05/KZ-22/KZ-23; migrate domain slices only after the DAL/model/error contracts are fixed. |
| [ ] | WEB KZ-23 | W1 | Remove ceremonial application-service hops; keep coordinators only for real workflows | Apply as part of KZ-01/KZ-05 slice migration; remove one-hop services only after their validation/orchestration responsibility has moved somewhere explicit. |
| [x] | WEB KZ-02 | W1 | Create a consistent daemon gateway boundary and centralize IPC exception handling | Completed in W1.2: feature gateways return `DaemonResult`, raw IPC/routes stay inside gateways, daemon-declared failures flow through non-throwing IPC results, and assertive failures are mapped once by the MVC exception filter. |
| [x] | WEB KZ-03 | W1 | Split rule metadata persistence reconciliation from signed daemon protocol parsing | Completed in W1.3: the rule daemon gateway emits explicit replacement reconciliation facts/plan state, while metadata persistence consumes only domain facts and preserves post-firewall reconciliation failure semantics. |
| [x] | WEB KZ-05 | W1 | Move REST request-shape validation out of business services and formalize request-to-domain mapping | Completed across W1 slice migrations and the W2.6 API-wide audit: DTOs own transport shape, mapping owns normalization, and domain validators own semantic checks. |
| [x] | WEB KZ-24 | W2 | Audit REST DTO validation and eliminate transport-shape checks from application logic | Completed in W2.6: full V1 DTO audit, declarative rule-metadata/known-host shape validation, trim-consistent annotations, duplicate guard removal, and retirement of obsolete Web-only firewall validators. |
| [ ] | WEB KZ-04 | W3 | Decompose `Startup` into feature registration and startup lifecycle units | Do late, after DAL/gateway/coordinator registrations stabilize; otherwise Startup would be decomposed twice. |
| [x] | WEB KZ-06 | W2 | Unify rule metadata/template tag and group dependency handling | Completed in W2.1: metadata/templates share persistence dependency resolution and tag-relation diffing in the final rules DAL, while retaining explicit slice-specific data-access operations. |
| [ ] | WEB KZ-22 | W1 | Standardize mutation/error propagation instead of feature-local outcome plumbing for common failures | Design alongside KZ-01 because DAL mutation signatures depend on the common error/result model; KZ-09 consumes the result. |
| [ ] | WEB KZ-07 | W2 | Reduce rule-group/rule-tag catalog copy-paste without generic-controller overengineering | Do after KZ-22 and KZ-23. Much of the current duplication disappears when local mutation enums and ceremonial services disappear. |
| [x] | WEB KZ-08 | W2 | Centralize semantic rule-ID extraction from daemon snapshots | Completed in W2.2 with a Web-local `LiveRuleIdentitySet` consumed by inventory, metadata reconciliation/liveness, batch cleanup, and replacement interpretation. |
| [ ] | WEB KZ-09 | W2 | Standardize HTTP error shape and declared response contracts | Do after KZ-22 and KZ-02. Fold KZ-15 into this centralized error mapping rather than patching the old mapper first. |
| [x] | WEB KZ-10 | W2 | Make authentication transaction ownership explicit and reduce service contracts tied to `IdentityUser` | Completed in W2.5: `AuthenticationFlowService` owns auth transactions; context-bound `RefreshTokenDataAccess` participates explicitly; refresh rotation no longer returns `IdentityUser`. |
| [ ] | WEB KZ-11 | W3 | Shrink accidental public surface area | Late cleanup after final service/DAL/gateway boundaries determine what truly needs to stay public. |
| [x] | WEB KZ-13 | W1 | Decide whether transient interface disappearance is allowed to erase user-owned metadata | Completed in W2.4: missing interfaces retain public identity/comment/visibility as non-present rows; reappearance revives them; explicit cleanup revalidates daemon presence before hard deletion. |
| [x] | WEB KZ-12 | W1 | Delete dead validation/signing implementations and minor stale code | Completed in W1.1 with narrowed scope: removed the unused RSA JWT key provider and stale group/tag repository imports. The reusable IPv4/port validation attributes and their tests are deliberately retained for KZ-05 request-DTO validation. |
| [x] | WEB KZ-16 | W2 | Extract auth cookie policy and Identity error mapping | Completed in W2.5 with one refresh-cookie policy and explicit Identity password-error translation into Identity-independent workflow validation fields. |
| [x] | WEB KZ-17 | W1 | Centralize daemon endpoint paths | Completed in W1.2.1: unsigned read/probe paths are private gateway details; signed mutation routes remain owned by shared request-message contracts. |
| [x] | WEB KZ-18 | W2 | Tighten bulk persistence operations after the boundary refactor | Completed in W2.2: final-DAL metadata bulk cleanup uses `ExecuteDeleteAsync`, returns affected-row counts, and preserves relation cleanup through database cascades. |
| [ ] | WEB KZ-19 | W3 | Treat the description-length migration as explicitly destructive history | Policy/documentation item; close before blitz exit, but do not rewrite an applied migration. |
| [ ] | WEB KZ-20 | W3 | Reduce repeated versioned-controller policy attributes only if conventions stay obvious | Last/optional declaration cleanup after the controller/error conventions stabilize. |
| [ ] | WEB KZ-21 | W2 | Prefer `ToListAsync` over `ToArrayAsync` for EF materialization when array identity is irrelevant | Do after KZ-01 migrations; delete obsolete materializations first, then change only the surviving array materializations. |

### `Ufw.Web.Client`

| Done | Source item | Planned wave | Finding | Sequencing note |
|---|---|---|---|---|
| [ ] | CLIENT KZ-01 | C2 | Move rule-page application workflows out of Razor | Do after C1 and after the Web API/shared-model checkpoint so workflows are extracted around stable contracts rather than current DTO/error quirks. |
| [ ] | CLIENT KZ-02 | C1 | Make client rule filtering consume the shared semantic-domain primitives | Requires PR #40. Do before KZ-16 so filter micro-clone cleanup is performed against the final semantic adapter. |
| [ ] | CLIENT KZ-03 | C1 | Centralize snapshot occurrence indexing and remove object-identity lookup | Do early; later rule workflows/navigation should consume the stable occurrence index instead of preserving ReferenceEquals paths. |
| [ ] | CLIENT KZ-04 | C1 | Collapse the repeated intent-signing pipeline and compatible-context lookup | Do only after daemon KZ-014/KZ-008 and Web KZ-02/KZ-03 stabilize intent and gateway behavior. |
| [ ] | CLIENT KZ-05 | C1 | Define the rule-order permutation invariant once | Do before ordering workflows are moved/refined; subsequent code should traffic in one validated permutation representation. |
| [ ] | CLIENT KZ-06 | C1 | Centralize metadata DTO-to-domain normalization | Do after Web shared-domain/metadata contracts stabilize; KZ-17 and later workflows should consume this one mapper. |
| [ ] | CLIENT KZ-07 | C3 | Converge desktop/mobile rule rendering onto shared behavior and fragments | Do after KZ-01 removes workflow behavior from page/component surfaces; then converge only presentation/interaction behavior. |
| [ ] | CLIENT KZ-08 | C2 | Move metadata-authoring CRUD out of `RuleMetadataEditor` and centralize metadata limits | Do after Web KZ-05 exposes shared limits and after Client KZ-15 settles catalog state semantics. |
| [ ] | CLIENT KZ-09 | C2 | Replace fragmented multi-boolean workflow state with explicit feature state | Do after KZ-01 extracts the create workflow; model the final workflow, not the current page flags. |
| [ ] | CLIENT KZ-10 | C2 | Factor the shared known-host/network-interface inventory page mechanics | Do after Web KZ-13 fixes interface lifecycle semantics and after feature-level inventory operations are stable. |
| [ ] | CLIENT KZ-11 | C1 | Remove repeated `TryDescribe(... out _)` + `Describe(...)` error classification | Do immediately after Web KZ-09 stabilizes ProblemDetails/error semantics, before moving more workflow code into Features. |
| [ ] | CLIENT KZ-12 | C1 | Stop localizing validator failures by exact English error text | Provider-side stable validation identities should be introduced at the shared-contract checkpoint; C1 then switches localization to those identities. |
| [ ] | CLIENT KZ-13 | C3 | Reclassify non-isolated component SCSS and extract generic menu/control styles | Do after KZ-07 so style ownership follows the final component decomposition. |
| [ ] | CLIENT KZ-14 | C3 | Consolidate dialog options and confirmation-dialog presentation shells | Do after workflow extraction so confirmation shells contain presentation only, not temporary workflow responsibilities. |
| [ ] | CLIENT KZ-15 | C1 | Simplify catalog state and define/remove `Version` | Do in C1 before KZ-08; metadata-authoring should be built on final catalog-state semantics. |
| [ ] | CLIENT KZ-16 | C1 | Reduce filter editor/evaluator/reconciler micro-clones without over-generalizing Razor | Do after KZ-02 removes the duplicated semantic algorithms; otherwise helpers would abstract code that is about to disappear. |
| [ ] | CLIENT KZ-17 | C1 | Separate protocol response mapping from `RuleSnapshot` | Do in C1 with KZ-06, before KZ-01; workflows should consume a transport-free RuleSnapshot. |
| [ ] | CLIENT KZ-18 | C2 | Split group-deletion planning from side-effect execution | Do after snapshot/index foundations, then let KZ-01 consume the planner/executor split rather than extracting it later. |
| [ ] | CLIENT KZ-19 | C2 | Make rule-editor reference-data failures explicit and symmetric | Do with feature workflow extraction, using the final server error contract and explicit reference-data results. |
| [ ] | CLIENT KZ-20 | C4 | Factor repeated HttpClient registration and resource-client mechanics | Do after server endpoints/contracts are stable and feature workflow extraction has stopped changing API-client call patterns. |
| [ ] | CLIENT KZ-21 | C4 | Audit and minimize the client's public surface | Late cleanup after final interfaces/callers are known. |
| [ ] | CLIENT KZ-22 | C4 | Consolidate small presentational clones opportunistically | Last-mile cleanup after component/workflow/style structure is final. |
| [ ] | CLIENT KZ-23 | C4 | Ensure consistent use of SimpleUriBuilder | Pair with KZ-20 while API/navigation URI construction is already being touched. |

## Completion rule

A wave is complete only when its source items' original acceptance criteria/definition-of-done requirements are satisfied, not merely when the broader refactor that contains them has landed. In particular, subsumed correctness items such as WEB KZ-14 and KZ-15 still require dedicated regression coverage, and contained cleanup items remain checklist entries even when their code naturally disappears during a larger change.

The kaizen blitz is complete when all 75 source items are checked, the three project-level definitions of done are satisfied, and the final repository integration gate is green.