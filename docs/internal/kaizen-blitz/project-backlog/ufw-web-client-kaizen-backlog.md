# Ufw.Web.Client Kaizen Audit

## Post-W acceptance follow-ups

See [ACC-02/04/05/06](../ufw-kaizen-plan.md#post-w-acceptance-remediation-october-2026).

- [x] **ACC-02:** share one WASM auth session across route authorization and `IHttpClientFactory` handler scopes.
- [ ] **ACC-02 acceptance:** cross-browser password-change/refresh-expiry and cross-tab coordination regression tests.
- [ ] **ACC-04 / KZ-11 (C1):** centralize 401/session-expired navigation, handling multiple concurrent failures once; keep 403 separate and do not redirect failed login. Test deep links and tabs.
- [x] **ACC-05 / KZ-18 (C2):** delete active group rules while retaining template-referenced group and templates; show references and precise confirmation wording. Preserve concurrent-membership safety checks.
- [x] **ACC-06 (C3):** full unmatched rule IDs in markup, accessibility and copy; presentation-only CSS truncation.

## Overall assessment

The client is generally disciplined. The main technical debt is concentrated rather than pervasive. The largest opportunities are:

1. application/workflow orchestration has accumulated in several Razor pages despite the documented `UI -> Features -> Api` boundary;
2. firewall network/port semantics are reimplemented in the client filtering layer; PR #40 introduced a formal shared semantic domain that should become the client's source of truth for those operations;
3. snapshot-local rule identity/position logic is duplicated and in two paths depends on CLR object identity;
4. intent signing, ordering invariants, and metadata protocol mapping each have multiple copies of the same correctness-sensitive logic;
5. desktop/mobile rule rendering is two implementations of largely the same component behavior, markup, and styles;
6. metadata authoring and several catalog/inventory pages repeat workflow/error/dialog/state machinery that should be factored at the appropriate feature/UI layer.

I found no reason for a wholesale rewrite. This is a good fit for a kaizen blitz: consolidate the correctness-sensitive duplicates first, then peel orchestration out of Razor, then clean up repeated presentation mechanics.

## Review baseline

The repository documents these relevant boundaries:

- `Api` is transport only; it serializes, sends, and maps protocol failures, but does not own user workflows (`docs/architecture/browser-application.md:42-44`).
- reusable behavior that does not need Razor lifecycle/DOM state belongs in `Features` (`docs/architecture/browser-application.md:44`, `docs/development/client-ui.md:32-34`).
- Razor pages should primarily coordinate lifecycle, navigation, dialogs, and presentation (`docs/architecture/browser-application.md:122-125`).
- CSS isolation is the default; non-isolated component SCSS is an explicit exception, and generic Mud/application behavior belongs under `UI/Styles/controls` (`docs/development/client-ui.md:36-46`, `62-66`).

### Baseline semantic-domain foundation: PR #40

[PR #40](https://github.com/frederik-hoeft/ufw-webui/pull/40) is merged into the kaizen baseline. It adds the formal read-only firewall-policy semantic domain under `Ufw.Shared.Domain`, including `NetworkAddress`, `PacketPorts`, `PacketPortSet`, generic `IntervalSet<T>` set algebra, packet-space types, and `FirewallPolicyProjector`.

This materially changes KZ-02. The client no longer needs new firewall range/set primitives invented specifically for the cleanup; the merged domain model already supplies the semantic representation and algebra that the filtering subsystem was independently implementing. KZ-02 should therefore be treated as an **integration/adaptation refactor over PR #40**, not as a new domain-model design exercise.

The architectural direction established by PR #40 should remain intact: normalized `Ufw.Shared.Firewall` state is projected **into** the semantic domain. Do not make `RuleSpecificationNormalizer` or the firewall mutation model depend upward on `Ufw.Shared.Domain` merely to share text parsing. Any remaining Firewall-vs-Domain lexical parsing duplication should be handled separately, if worthwhile, by extracting neutral syntax/value primitives below both layers.

## Priority model

- **P1**: correctness-sensitive duplication, architectural coupling, or a refactor that materially reduces future regression risk. Do these in the blitz.
- **P2**: meaningful maintainability debt with a bounded refactor. Do after the P1 foundation, or in parallel where conflicts are low.
- **P3**: worthwhile cleanup, but not a reason to delay the higher-leverage work.

---

## P1 findings

### KZ-01: Move rule-page application workflows out of Razor

- [x] Centralize rule inventory loading/response mapping and rule metadata updates in `Features`; remove direct rule REST client injection from the four pages.
- [x] Extract create/edit signed-mutation sequencing, post-mutation reconciliation, and list mutation workflows; keep UI lifecycle and presentation in Razor.
- [x] Complete the code-level acceptance criteria below before checking off KZ-01; retain deferred end-to-end/manual acceptance in the cross-project plan.

**Where**

- `UI/Pages/RulesPage.razor:6-26`, `RulesPage.razor.cs:136-620`
- `UI/Pages/CreateRule.razor:6-20`, `CreateRule.razor.cs:134`, `446+`
- `UI/Pages/EditRule.razor:5-14`, `EditRule.razor.cs:113`, `258+`
- `UI/Pages/RuleGroupsManagement.razor:6-14`, `RuleGroupsManagement.razor.cs:227+`

**Problem**

The rule pages still coordinate mutation reconciliation, template persistence, orphan-group cleanup, navigation-context construction, known-host refresh, stale/fresh inventory transitions, and result interpretation. These concerns need focused workflow boundaries rather than page-owned application orchestration.

The repository explicitly says behavior that is testable without a renderer belongs in `Features`. These pages have become application-service composition roots rather than thin presentation coordinators.

**Refactor**

Create focused feature-level workflows, not one giant page service. Likely seams:

- a rule inventory loader/refresher that returns the existing `RuleInventoryState` transitions;
- a rule metadata mutation service that owns `UpdateRuleMetadataRequest`, response validation, and snapshot application;
- create/edit workflow coordinators for the multi-step signed mutation + authoritative reconciliation + optional metadata sequence;
- a small rule-list workflow for disable/delete and post-mutation refresh where it can remain independent of dialog composition.

Keep dialog creation, navigation, snackbars, and lifecycle cancellation in Razor. Move API DTO construction, mutation sequencing, and domain state decisions out.

**Acceptance criteria**

- `RulesPage`, `CreateRule`, `EditRule`, and `RuleGroupsManagement` no longer inject `IRuleApiClient` directly;
- page code-behind does not construct rule REST requests;
- workflow behavior is unit-testable without bUnit/renderer setup;
- collaborator counts drop materially, especially on `RulesPage` and `CreateRule`.
- Razor pages own presentations, small UI projections, localization, UI state, and component lifecycle management, domain logic belongs in feature-level workflows.

### KZ-02: Make client rule filtering consume the shared semantic-domain primitives

- [x] Use the shared network and port interval model through a filter-local adapter in editors, evaluators, and known-host projection.
- [ ] Complete the deferred full client-suite/release acceptance gate.

**Where**

Current client duplicates:

- `Features/Rules/Filtering/Networks/RuleNetwork.cs:32-125`
- `Features/Rules/Filtering/Ports/RulePortSet.cs:18-105`
- `Features/Rules/Filtering/Networks/NetworkRuleFilterEvaluator.cs`
- `Features/Rules/Filtering/Ports/PortRuleFilterEvaluator.cs`
- `UI/Components/Rules/Filtering/Networks/NetworkRuleFilterEditor.razor.cs`
- `UI/Components/Rules/Filtering/Ports/PortRuleFilterEditor.razor.cs`
- known-host/rule endpoint projection paths that consume the same network relationship semantics

Shared semantics introduced by pending PR #40:

- `Ufw.Shared/Domain/NetworkAddress.cs`
- `Ufw.Shared/Domain/PacketPorts.cs`
- `Ufw.Shared/Domain/PacketPortSet.cs`
- `Ufw.Shared/Domain/Algebra/Interval.cs`
- `Ufw.Shared/Domain/Algebra/IntervalSet.cs`
- `Ufw.Shared/Domain/Projection/FirewallPolicyProjector.cs`

**Problem**

The filtering subsystem currently has its own network and port semantic implementation. `RuleNetwork` parses/masks IPv4 and IPv6 CIDRs, rejects scoped IPv6, canonicalizes prefixes, and implements containment. `RulePortSet` parses lists/ranges, sorts and coalesces them, formats a canonical representation, and implements overlap.

PR #40 introduces essentially the same semantic foundation as first-class shared domain code:

- `NetworkAddress.ParseIPv4` / `ParseIPv6` map hosts and CIDRs to canonical numeric intervals;
- `PacketPorts.Parse` maps UFW port syntax to normalized `IntervalSet<ushort>` values;
- `IntervalSet<T>` owns overlap, intersection, subtraction, union, containment/superset, and canonical range coalescing;
- `PacketPortSet` carries protocol-aware port applicability for the broader policy model.

The current client copies already differ behaviorally from the new domain. For example, the new `PacketPorts` parser rejects leading-zero values such as `08`, while the existing client parser accepts them as port 8. Maintaining both implementations would therefore preserve exactly the semantic drift this finding is intended to remove.

**Refactor**

After PR #40 is merged, introduce a **small feature-local front-end semantic adapter** under the rule-filtering feature, rather than creating another generic service or new firewall-domain model. A suitable shape is: 

```text
Features/Rules/Filtering/Semantics/
    IRuleFilterSemanticService.cs
    RuleFilterSemanticService.cs
    NetworkFilterOperand.cs
    PortFilterOperand.cs
```

The adapter should expose filter-oriented operations while hiding the IPv4 `uint` / IPv6 `UInt128` generic split from Razor and filter evaluators. Conceptually it should provide operations such as:

```csharp
bool TryParseNetwork(
    string? value,
    FirewallAddressFamily family,
    out NetworkFilterOperand? operand);

bool TryParsePorts(
    string? value,
    out PortFilterOperand? operand);
```

The operands may carry both the shared semantic set and a canonical **presentation** value. The presentation string is intentionally an adapter concern: `NetworkAddress` and `IntervalSet<T>` are evaluation-oriented and their `ToString()` output is not UFW/UI syntax. Formatting ports from normalized intervals is fine; formatting must not reintroduce a second parser or set implementation.

Migrate these consumers to the adapter/shared domain:

- `NetworkRuleFilterEditor` and `PortRuleFilterEditor` for parsing/validation;
- `NetworkRuleFilterEvaluator` for equality/containment relationships;
- `PortRuleFilterEvaluator` for overlap;
- known-host/rule endpoint projection for network overlap/containment;
- any future semantic rule-search/exploration UI that needs the same operands.

`RuleNetwork` and `RulePortSet` should disappear, or at most remain as thin client-facing wrappers around shared domain sets with **no independent CIDR/port parsing, masking, merging, containment, or overlap algorithms**.

Do **not** make `RuleSpecificationNormalizer` depend on `Ufw.Shared.Domain`. PR #40 deliberately establishes the opposite architectural direction: normalized firewall state is projected into the semantic domain. There is still some lexical parsing/normalization overlap between `Ufw.Shared.Firewall` and `Ufw.Shared.Domain`, but that is a separate lower-layer cleanup. If we later want one repository-wide text parser, extract neutral network/port syntax primitives below both layers rather than reversing the Firewall -> Domain boundary.

**Acceptance criteria**

- no CIDR masking/containment algorithm remains in `Ufw.Web.Client`;
- no port-list parsing, range coalescing, or overlap algorithm remains in `Ufw.Web.Client`;
- client filtering uses PR #40's `NetworkAddress`, `PacketPorts`, and `IntervalSet<T>` semantics through one feature-local adapter;
- network/port filter editors, evaluators, and known-host projection use that same adapter rather than separate parsing paths;
- canonical UI formatting is presentation-only and does not determine semantic acceptance;
- tests cover IPv4/IPv6 hosts and CIDRs, `/0`, scoped IPv6 rejection, family mismatch, adjacent/overlapping port ranges, malformed ranges/empty segments, and leading-zero behavior;
- the existing Firewall -> Domain projection dependency remains one-way.

**Non-blocking follow-up**

After this client cleanup, reassess the residual parsing overlap among `RuleSpecificationValidator`, `RuleSpecificationNormalizer`, `NetworkAddress`, and `PacketPorts`. Consolidate it only if a neutral shared syntax/value layer can reduce duplication without coupling the firewall mutation model to the semantic evaluation domain.

### KZ-03: Centralize snapshot occurrence indexing and remove object-identity lookup

- [x] Use snapshot-local occurrence IDs, shared family positions, and semantic identity multiplicity in navigation and projections.

**Where**

- `Features/Rules/Insertion/RuleInsertionNavigationService.cs:21-40`, `96-121`
- `Features/Rules/Replacement/RuleReplacementNavigationService.cs:18-41`, `133-158`
- `UI/Pages/RulesPage.razor.cs:525`
- related indexing already exists in `Features/Rules/RuleListProjectionService.cs`

**Problem**

Insertion and replacement independently compute family-local positions and locate a target occurrence by scanning the snapshot with `ReferenceEquals`. `RulesPage` performs another `ReferenceEquals` membership check before ordered insertion.

Reference identity is an accidental coupling to the exact object instance returned by the current snapshot/projection path. A semantically identical or rehydrated `ListedFirewallRule` is treated as “not present.” This is also duplicated correctness-sensitive snapshot-coordinate logic.

**Refactor**

Introduce one snapshot-local index/locator, for example `RuleSnapshotIndex`, built once from the authoritative ordered list. It should expose:

- occurrence ID/index;
- family and one-based family position;
- semantic `RuleId` multiplicity/uniqueness;
- lookup by occurrence ID plus optional rule-ID/fingerprint checks.

`RuleRowProjection` already carries occurrence/position information. Navigation should pass stable snapshot-local coordinates rather than asking services to rediscover them from object references.

**Acceptance criteria**

- no rule-navigation or mutation path relies on `ReferenceEquals` for snapshot membership;
- family-position calculation has one implementation;
- insertion/replacement resolution shares the same occurrence index and fingerprint context.

### KZ-04: Collapse the repeated intent-signing pipeline and compatible-context lookup

- [x] Centralize typed signing/envelope construction and compatible intent-context validation.

**Where**

- `Features/Rules/Intent/BrowserIntentSigningService.cs:17-32`, `48-67`, `89-103`, `129-143`, `169-183`, `203-222`
- `Features/Rules/Intent/RuleMutationService.cs:101-110`
- `Features/Rules/Ordering/RuleOrderingService.cs:26-35`

**Problem**

All six signing methods repeat the same algorithm: validate input, derive key ID, construct unsigned envelope with nonce/time/protocol operation, serialize typed payload, canonicalize, sign, and return the signed request. `RuleMutationService` and `RuleOrderingService` also duplicate compatible intent-context retrieval/version validation.

This is cryptographic/protocol glue. Duplication is particularly undesirable because a future field, canonicalization precondition, timestamp rule, or protocol-version change must be applied everywhere identically.

**Refactor**

- add a typed internal signing helper that owns the common envelope/sign/return pipeline while each operation supplies its typed payload serializer/canonicalizer;
- extract an `ICompatibleIntentContextProvider`, or fold ordering execution into the same mutation execution abstraction if that produces a cleaner responsibility boundary.

Keep operation-specific validation explicit. The common helper should not erase the distinct typed payloads.

**Acceptance criteria**

- key ID/nonce/time/signature envelope code has one implementation;
- protocol compatibility checking has one implementation;
- operation-specific tests still verify the exact canonical bytes and serialized payload for each mutation type.

### KZ-05: Define the rule-order permutation invariant once

- [x] Use one validated, immutable permutation across preview, signed execution, and result projection.

**Where**

- `Features/Rules/Ordering/RuleOrderingPreview.cs:32-45`
- `Features/Rules/Ordering/RuleOrderingService.cs:37-54`
- `Features/Rules/Ordering/RuleOrderingResultProjectionService.cs:72-90`

**Problem**

Three implementations verify that an order is a valid complete permutation. They differ only in input/count shape and exception wording. This is one invariant, duplicated across preview, execution, and result projection.

**Refactor**

Create a single `RuleOrderPermutation` value/validator or a small internal `RuleOrderingContract.ValidatePermutation(...)`. Prefer constructing a validated value once and passing it through rather than repeatedly validating raw `int[]` values.

**Acceptance criteria**

- one implementation checks count/range/uniqueness;
- preview, signed request execution, and result projection consume the same validated representation or validator.

### KZ-06: Centralize metadata DTO-to-domain normalization

**Where**

- `Features/Rules/Metadata/RuleTagCatalogService.cs:50-82`
- `Features/Rules/RuleSnapshot.cs:101-143`
- `Features/Rules/Metadata/RuleMetadataReconciliationService.cs:39-66`
- `Features/Rules/Templates/RuleTemplateCatalogService.cs:100-157`

**Problem**

Tag and group protocol validation/mapping is repeated in several contexts: nonempty IDs/names, trimming, color normalization, duplicate checks, optional comment handling, and ordering. The copies are not identical because each endpoint adds context-specific checks, but the primitive `RuleTagItem -> RuleTag` and `RuleGroupSummary -> RuleGroupMembership` rules are the same.

This is protocol-domain mapping with a single semantic meaning. Leaving it duplicated increases the chance that one endpoint accepts data another rejects.

**Refactor**

Extract a feature-level metadata protocol mapper with small composable operations such as `MapTag`, `MapGroupReference`, and `MapRuleMetadata`. Keep collection-specific uniqueness checks at the calling endpoint where the policy differs.

Also move the response factories out of `RuleSnapshot`; see KZ-18.

**Acceptance criteria**

- tag color/name/ID validation is implemented once;
- group-reference mapping is implemented once;
- endpoint-specific duplicate/collection rules remain explicit and tested.

### KZ-07: Converge desktop/mobile rule rendering onto shared behavior and fragments

**Where**

- `UI/Components/Rules/RuleDesktopRow.razor(.cs/.scss)`
- `UI/Components/Rules/RuleMobileCard.razor(.cs/.scss)`
- `UI/Components/Rules/RuleDesktopContent.razor(.cs/.razor.scss)`
- `UI/Components/Rules/RuleMobileContent.razor(.cs/.scss)`

**Evidence**

A token-shingle clone pass found:

- `RuleMobileCard.razor.cs` vs `RuleDesktopRow.razor.cs`: ~0.68 Jaccard similarity, ~0.86 smaller-file containment;
- their Razor markup: ~0.58 Jaccard, ~0.76 containment;
- `RuleDesktopRow.scss` vs `RuleMobileCard.scss`: ~0.43 Jaccard, ~0.70 containment;
- desktop/mobile content code-behind: ~0.89 smaller-file containment.

The two row shells repeat the same large parameter set, drag/drop behavior, metadata expansion, action wiring, rule-number/position labels, and similar state-to-class logic.

**Problem**

Desktop table and mobile card layouts legitimately differ, but interaction semantics are implemented twice. Every new row action or state therefore requires coordinated edits in two independent components and often two style files.

**Refactor**

Keep the layout shells separate, but extract shared units:

- one row interaction/state model or reusable component for drag/action/metadata behavior;
- shared visual fragments for metadata/details/actions where the rendered structure is the same;
- a common rule-content component or presentation model for endpoint/protocol/comment/action rendering;
- shared style primitives for identical rule-row visual concepts, leaving table/card layout isolated.

Avoid a deep inheritance hierarchy of Razor components. Composition plus a small C# state object is likely cleaner.

**Acceptance criteria**

- adding a rule action or metadata interaction does not require implementing the behavior twice;
- action/color/position formatting has one implementation;
- desktop and mobile retain independent layout markup only where their DOM structures genuinely differ.

### KZ-08: Move metadata-authoring CRUD out of `RuleMetadataEditor` and centralize metadata limits

**Where**

- `UI/Components/Rules/Metadata/RuleMetadataEditor.razor.cs:11`, `35-193`
- `UI/Components/Rules/Metadata/ManageRuleTagsDialog.razor.cs:10`, `99+`
- `UI/Components/Rules/Metadata/EditRuleGroupDialog.razor.cs:10`, `24+`
- `UI/Components/Hosts/EditKnownHostDialog.razor.cs:10-12`

**Problem**

`RuleMetadataEditor` is not just an editor surface. It refreshes tag/group catalogs, creates new catalog entries, generates tag colors, performs duplicate-name detection, builds autocomplete option models, and maps exceptions. That is application behavior embedded in a reusable UI component.

The UI also hard-codes contract limits (`64` for tag/group metadata names, `128/64/200` for known hosts) in multiple places. Known-host limits already exist privately in `KnownHostRequest`; metadata limits are not represented as shared contract constants at all.

**Refactor**

- add a feature-level metadata-authoring service/model that owns catalog search/create/reconcile behavior;
- expose shared rule-tag/group limit constants/validation from the appropriate contract/domain assembly;
- expose known-host limits from the wire/domain contract instead of duplicating them in the dialog;
- keep editor-local transient input state and UI option rendering in the component.

**Acceptance criteria**

- `RuleMetadataEditor` does not call catalog mutation methods directly;
- no UI component owns magic domain length limits;
- tag/group/known-host validation rules are sourced from one contract/domain definition.

---

## P2 findings

### KZ-09: Replace fragmented multi-boolean workflow state with explicit feature state

**Where**

- `UI/Pages/CreateRule.razor.cs`
- `UI/Pages/EditRule.razor.cs`
- `UI/Pages/RuleTemplateEditorPage.razor.cs`
- several CRUD/catalog pages

**Problem**

The client already has good explicit state models such as `RuleInventoryState`, `RulesPageInteractionState`, and `RuleEditWorkflowState`. `CreateRule` still coordinates a large set of booleans/flags such as initialization, template loading/handling, submitting, ordered-insertion invalidation, and uncertain mutation completion. Template/catalog pages use similar `_loading`/`_saving`/`_notFound`/`_errorFromSave` combinations.

The smell is not “booleans are bad”; it is that workflow legality is encoded implicitly across many flags while adjacent rule flows already use explicit state transitions.

**Refactor**

After KZ-01, represent the high-risk create workflow as an explicit state/transition model in `Features`. Apply the same technique to other workflows only where it eliminates illegal combinations; do not build a generic state-machine framework.

### KZ-10: Factor the shared known-host/network-interface inventory page mechanics

**Where**

- `UI/Pages/KnownHostsPage.razor(.cs)`
- `UI/Pages/NetworkInterfacesPage.razor(.cs)`

**Evidence**

Their Razor files share more than 500 token shingles. Their code-behind repeats lifetime CTS ownership, load/save/error state, refresh guards, cancellation handling, error mapping, formatting, visibility/update plumbing, and very similar page shells.

**Refactor**

Extract the smallest reusable pieces:

- an async inventory operation state/helper for refresh/mutation/cancellation/error handling;
- common inventory header/loading/error/empty presentation components if their DOM is genuinely identical;
- move `SetVisibility`/comment-update request creation into feature inventory services so pages express intent rather than REST request shape.

Do **not** create a generic “CRUD page” base class that erases the useful domain differences.

### KZ-11: Remove repeated `TryDescribe(... out _)` + `Describe(...)` error classification

**Where**

Dozens of page/component catch blocks, including `RulesPage`, `CreateRule`, `EditRule`, `KnownHostsPage`, `NetworkInterfacesPage`, metadata dialogs, and `RuleEditorReferenceDataService`.

**Problem**

The common pattern uses `ClientErrors.TryDescribe(exception, out _)` in a catch filter and then immediately calls `ClientErrors.Describe(exception)`, classifying the same exception twice. It also produces large amounts of repetitive exception/cancellation boilerplate in UI code.

**Refactor**

Give the error mapper a filter-friendly `CanDescribe(Exception)` if that is the desired pattern, or introduce a narrow UI operation helper/state runner that maps known failures once while preserving the rule that unexpected programmer exceptions are not silently swallowed. Do not replace all catches with a broad catch-and-map helper that hides unexpected errors.

Also remove the unused parameter from `ClientErrorMapper.DescribeProtocolError` if it remains unnecessary.

**Contract decision:** The Web error contract is ProblemDetails-based. Client fallback uses HTTP status for empty or incompatible bodies; no legacy `{ message }` parser is retained. API 401s use one authentication navigation policy; 403 and login credential failures remain distinct.

### KZ-12: Stop localizing validator failures by exact English error text (complete)

**Where**

- `Features/Rules/Authoring/RuleValidationMessageLocalizer.cs:10-38`

**Problem**

The localizer keys a dictionary by exact English strings emitted by the shared validator. Any wording change in `RuleSpecificationValidator` silently breaks localization and falls back to the English message. This is tight coupling across assemblies to presentation text rather than a stable error identity.

**Refactor**

S1 has completed the provider-side dependency: shared firewall validation now returns a stable open string code alongside the field and human-readable diagnostic, current producers populate that code, and older application-v1 payloads without a code remain readable.

C1 should switch `RuleValidationMessageLocalizer` to key resources by the stable code and keep the English diagnostic only as fallback/debug text. Do not redefine the shared code vocabulary in the client.

W2.7.1 completed the browser-visible transport dependency as well: the public ProblemDetails contract carries structured validation entries with the stable code, and `ApiRequestException.ValidationErrors` preserves them instead of flattening them to message text. C1 therefore does not need another HTTP parsing change before switching localization to the shared code.

### KZ-13: Reclassify non-isolated component SCSS and extract generic menu/control styles

**Where**

- `UI/Styles/app.scss:11-37`
- 26+ colocated non-isolated component SCSS imports
- especially `UI/Components/Hosts/KnownHostActionsMenu.scss` and `UI/Components/Rules/RuleActionsMenu.scss`

**Evidence**

The two action-menu styles differ only in owner class names and are ~0.86 Jaccard / ~0.93 containment. The repository says generic Mud/application behavior belongs in `UI/Styles/controls`, while non-isolated component SCSS is exceptional.

**Problem**

Some global component Sass is absolutely justified because Mud popovers render outside the component subtree. But the global footprint is broader than the documented default, and repeated framework styling has begun to appear in owner-specific files.

**Refactor**

Classify every non-isolated file:

1. normal owner DOM -> convert to `.razor.scss` isolation;
2. intentional portal/overlay hook -> keep a small global owner-specific hook;
3. generic Mud/application behavior -> move to `UI/Styles/controls`.

Start with the action-menu clone by extracting one shared menu primitive. Do not mechanically convert portal/autocomplete styles to isolation. C3.3 has a per-import [ownership inventory](../client-style-ownership.md), and moves the exact menu clone and shared desktop/mobile row fragments without changing owner layout. C3.4 migrates six owner-local leaf styles with scoped-CSS verification. C3.5 isolates the remaining safe dialog, endpoint and option components, including narrowly scoped descendant selectors for Mud-rendered buttons/icons. The completed ownership inventory documents why the remaining row/table, layout, portal, and mixed details styles are intentionally global. KZ-14 may revisit shared confirmation presentation, but the KZ-13 source-level audit is complete; visual integration acceptance is still outstanding.

### KZ-14: Consolidate dialog options and confirmation-dialog presentation shells

**Where**

- repeated `DialogOptions` definitions across rule, host, interface, metadata, template pages/components;
- `CleanupRuleMetadataDialog.razor` vs `DeleteRuleTagDialog.razor` is a strong markup clone;
- delete-confirmation code-behind appears in known-host/tag/template/group/rule variants.

**Problem**

Modal sizing/close behavior and common confirmation markup are copied repeatedly. The domain-specific confirmation logic is mostly small, but presentation changes have a broad edit surface.

**Refactor**

- create named `ClientDialogOptions` presets for common dialog categories;
- extract a reusable confirmation/impact shell component with slots/parameters for title/body/warnings/actions;
- keep domain validation and destructive-operation semantics in the concrete dialog.

**C3.6:** Shared `ClientDialogOptions` factory presets now own modal width, backdrop, Escape and first-child focus configuration, including the intentionally different filter-editor policy. `ConfirmationDialog` owns the identical simple destructive-confirmation body and action presentation for orphaned-metadata cleanup and tag deletion. Their callbacks remain in the concrete dialogs; richer signed, catalog and host confirmations retain their distinct domain-specific content and controls. The reconciliation dialog still passes the complete unmatched metadata identifiers; no truncation or mutation code is changed.

### KZ-15: Simplify catalog state and define/remove `Version`

**Where**

- `Features/Rules/Metadata/RuleTagCatalogService.cs`
- `Features/Rules/Metadata/RuleGroupCatalogService.cs`
- `Features/Rules/Templates/RuleTemplateCatalogService.cs`
- `IRule*CatalogService` interfaces

**Problem**

The three catalogs repeat `Current`, refresh/mutation replacement, normalization, and CRUD cache updates. Their `Version` values increment on create/update/delete but not on `RefreshAsync`, and production code does not consume the version. That gives `Version` ambiguous semantics: it is neither a revision of `Current` nor a server version.

**Refactor**

Remove `Version` if it exists only for tests/historical behavior. If a revision counter is actually required, rename it to make semantics explicit and update it for every `Current` replacement. A tiny `CatalogState<T>` helper may be useful, but do not force the typed catalogs into a generic repository abstraction.

### KZ-16: Reduce filter editor/evaluator/reconciler micro-clones without over-generalizing Razor

**Where**

- action/direction/protocol filter editors and evaluators;
- tag/group filter editors;
- `RuleTagFilterReconciler` vs `RuleGroupFilterReconciler`.

**Evidence**

Action/direction/protocol editor code-behind has the same load/sync/build skeleton. Tag/group editors have matching catalog-backed state and autocomplete logic. Tag/group reconcilers are near structural copies.

**Refactor**

Extract small typed C# helpers for enum editor state and catalog-backed ID reconciliation. Keep the explicit Razor components so labels/types remain clear. A generic Razor inheritance hierarchy would likely cost more than it saves.

### KZ-17: Separate protocol response mapping from `RuleSnapshot`

**Where**

- `Features/Rules/RuleSnapshot.cs:28-43`, `101-165`

**Problem**

`RuleSnapshot` currently represents immutable authoritative state, applies metadata mutations/reconciliation, validates protocol DTOs, and constructs itself from multiple REST response shapes. This couples the state object to `Ufw.Web.Model` and `ApiProtocolException` and contributes directly to the metadata mapping duplication in KZ-06.

**Refactor**

Move response construction/validation into `RuleSnapshotFactory`/`RuleInventoryMapper`. Keep snapshot-local transformations that preserve the snapshot abstraction on `RuleSnapshot` or in a dedicated reconciler.

### KZ-18: Split group-deletion planning from side-effect execution

**Where**

- `Features/Rules/Metadata/RuleGroupDeletionWorkflowService.cs`

**Problem**

The service is correctly located in `Features`, but one method owns candidate calculation, snapshot membership resolution, preview consistency checks, catalog race revalidation, signed batch deletion, and post-mutation cleanup/conflict interpretation. It is a valid workflow, but the pure decision logic and I/O are interleaved.

**Refactor**

Extract a deterministic `RuleGroupDeletionPlan`/planner for membership/precondition calculation, then keep the workflow service as the executor. This makes race/precondition behavior much easier to test and read without changing the external workflow.

### KZ-19: Make rule-editor reference-data failures explicit and symmetric

**Where**

- `Features/Rules/Authoring/RuleEditorReferenceDataService.cs`

**Problem**

Known-host refresh failures are effectively reduced to an empty/current fallback, while network-interface failures are carried as an explicit `InterfaceInventoryError`. A failed host catalog can therefore look like “there are no host suggestions,” which is different from an unavailable catalog.

**Refactor**

Return explicit load results/warnings for both reference-data sources and let `RuleEditor` decide how to present degraded authoring data. This also provides a natural place to remove repeated error-classification boilerplate.

### KZ-24: Integrate stale network-interface metadata into the cleanup workflow

**Where**

- `UI/Pages/RuleMetadataManagement.razor(.cs)`
- `UI/Components/Rules/Metadata/ReconcileRuleMetadataDialog.razor(.cs)`
- `Features/NetworkInterfaces/NetworkInterfaceInventoryService.cs`
- `Api/NetworkInterfaces/*`
- interface/rule metadata localization resources

**Problem**

WEB KZ-13 is complete in W2.4: a temporarily missing daemon interface retains its application-owned public identity, comment, and visibility in a non-present row, while normal interface inventory remains present-only. The server exposes retained candidates via `GET /api/v1/network-interfaces/stale` and race-safe permanent cleanup via `POST /api/v1/network-interfaces/stale/cleanup`; cleanup revalidates daemon presence before hard deletion. The client already has an analogous rule-metadata reconciliation flow with refresh, selectable orphan rows, confirmation, and bulk cleanup, but that flow is currently rule-metadata-specific.

**Refactor**

Extend the management cleanup experience to surface stale network-interface metadata alongside orphaned rule metadata without forcing both domains into one REST DTO. Reuse the existing cleanup interaction pattern and the dialog/operation primitives produced by KZ-10/KZ-14. Show enough retained interface context to make deletion reviewable (at minimum name, comment, and visibility), allow selected stale rows to be permanently purged, and refresh candidates after cleanup. Interfaces that have reappeared must disappear from the stale candidate set and must not be deletable through a stale cleanup race.

The normal `/interfaces` inventory and rule-editor reference data should continue to contain only currently present interfaces. The cleanup UI is management of retained application state, not another way to select stale interfaces for authoring.

**Acceptance criteria**

- stale interface metadata can be inspected and selectively purged from the same management cleanup flow/pattern used for orphaned rule metadata;
- rule-metadata and interface cleanup may use separate API contracts/services underneath; presentation reuse must not create a generic cross-domain repository/API abstraction;
- reappeared interfaces preserve their identity/metadata and are no longer cleanup candidates;
- cleanup revalidates authoritative daemon presence and cannot delete an interface that became present after the candidate list was loaded;
- localization and confirmation/selection behavior are covered alongside the existing cleanup UI.

**Sequencing:** implement after KZ-10 and KZ-14 have established the shared inventory-operation and confirmation/dialog primitives, so this feature does not create another temporary cleanup shell.

---

## P3 findings

### KZ-20: Factor repeated HttpClient registration and resource-client mechanics

**Implemented in C4.1.** Shared typed-client registration preserves anonymous health, cookie-only auth, and bearer-then-cookie chains. The GUID resource path helper centralizes validation and SimpleUriBuilder usage without moving JSON contracts out of typed clients.

**Where**

- `Program.cs:76-117`
- CRUD clients under `Api/KnownHosts`, `Api/NetworkInterfaces`, `Api/RuleTags`, `Api/RuleGroups`, `Api/RuleTemplates`
- `Api/ManagementApiHealthClient.cs` and `Api/Status/DaemonStatusApiClient.cs`

**Problem**

The DI block repeats base-address configuration and the same bearer/browser-credentials handler chain for most clients. Resource clients also repeat ID validation, URI construction, JSON content creation, and required-response parsing.

**Refactor**

Add small registration helpers for the established HTTP policies and a few request/URI helpers. Avoid a generic repository/base-client hierarchy; typed clients are otherwise clear and appropriately small.

### KZ-21: Audit and minimize the client's public surface

**Where**

Public interfaces are inconsistent across `Api`, `Features`, and `Services` even though implementations are internal and `_friends.cs` already grants test access.

**Problem**

Examples such as `IAuthApiClient`, `IKnownHostApiClient`, `INetworkInterfaceApiClient`, `IRuleTagApiClient`, and `IRuleGroupApiClient` are public while several adjacent API abstractions are internal. There is no production consumer outside the client project in this repository.

**Refactor**

Make implementation-detail interfaces internal unless there is an intentional external assembly contract. Keep only genuinely cross-assembly API public. This is low-risk cleanup and clarifies architectural boundaries.

### KZ-22: Consolidate small presentational clones opportunistically

**Where / examples**

- `UI/Components/UnexpectedError.razor` vs `StartupFailure.razor`;
- repeated local date/time formatting in inventory/status/rules pages;
- repeated action/menu/detail fragments detected by the clone pass;
- `Globals/UriHelpers.cs` plus a global static import is used only in a couple of template navigation calls.

**Refactor**

Use existing standalone/inventory primitives where they genuinely reduce duplication, introduce a shared formatting helper only for stable application-wide formatting rules, and remove one-off global indirection that does not buy readability. Do this only after the higher-leverage items; these are not architectural blockers.

### KZ-23: Ensure consistent use of SimpleUriBuilder

**Implemented in C4.1.** API resource identifiers and rule/template navigation queries consistently use SimpleUriBuilder. Constant endpoints and explicit URI validation/parsing are left as-is; the one-off globally imported UriOf alias is removed.

Route construction and URI manipulation should consistently use `SimpleUriBuilder` and `SimpleUriParser` to avoid ad-hoc string concatenation and parsing, ensuring correctness and maintainability across the client codebase.

Where: API layer and any client-side routing/navigation code

---

## Clone inventory worth addressing

The clone pass is a signal, not a mandate to abstract. These are the strongest pairs/groups and the recommended treatment:

| Clone | Strength | Recommendation |
| --- | --- | --- |
| `KnownHostActionsMenu.scss` / `RuleActionsMenu.scss` | near-identical | shared control/menu primitive, KZ-13 |
| `RuleMobileCard.razor.cs` / `RuleDesktopRow.razor.cs` | very high | shared row interaction behavior, KZ-07 |
| `RuleMobileCard.razor` / `RuleDesktopRow.razor` | high | extract identical fragments, retain layout shells |
| `RuleDesktopRow.scss` / `RuleMobileCard.scss` | high shared subset | shared rule-row visual primitives |
| `RuleDesktopContent.razor.cs` / `RuleMobileContent.razor.cs` | high containment | one content/presentation implementation |
| `CleanupRuleMetadataDialog.razor` / `DeleteRuleTagDialog.razor` | high | common confirmation shell, KZ-14 |
| `DeleteKnownHostDialog` / `DeleteRuleTagDialog` / `DeleteRuleTemplateDialog` code-behind | high | common dialog mechanics only |
| `KnownHostsPage.razor` / `NetworkInterfacesPage.razor` | large shared shell | inventory primitives/state helper, KZ-10 |
| Action/Direction/Protocol filter editors/evaluators | structural clone | small generic C# helper, KZ-16 |
| Tag/Group filter reconcilers | structural clone | shared ID-reconciliation helper, KZ-16 |
| `UnexpectedError.razor` / `StartupFailure.razor` | moderate | reuse standalone error shell if simple |

## Areas that do **not** need a blitz refactor

The audit did not find meaningful debt requiring action in these areas:

- `Services` is generally respecting the domain-agnostic boundary. Theme, culture, storage, clipboard, and authentication browser coordination are focused.
- the authentication coordination/HTTP replay design is intentionally specialized and does not show the same orchestration leakage as the rule pages.
- JavaScript modules (`startup.js`, authentication coordination, intent-signing bridge) are small and purpose-specific. The startup strings necessarily exist before Blazor localization is available; centralizing them into RESX would not improve the startup path.
- localization resource key parity is clean between neutral and de-DE resources.
- Sass compiler registration is internally consistent: all current isolated Sass sources are configured.
- small records/enums/interfaces throughout `Features/Rules/Filtering`, state transitions, navigation result models, and DTO-like presentation records are appropriately small and should not be merged merely to reduce file count.
- the project has good async hygiene: no `async void`, blocking task waits, or ad-hoc thread-pool workarounds were found.

## Recommended kaizen-blitz sequence

### Wave 1: correctness-sensitive foundations

1. KZ-02 client semantic adapter over the merged `Ufw.Shared.Domain` foundation (remove client CIDR/port algebra).
2. KZ-03 snapshot occurrence index / remove object identity.
3. KZ-04 signing/context helper.
4. KZ-05 ordering permutation value/invariant.
5. KZ-06 metadata protocol mapper.
6. KZ-12 structured validation error codes.

This wave deliberately attacks logic that can diverge semantically before moving presentation code around.

### Wave 2: workflow boundaries

1. KZ-01 page workflow extraction, starting with metadata/inventory and then create/edit/list mutations.
2. KZ-08 metadata-authoring service and shared limits.
3. KZ-09 create-rule state model.
4. KZ-17 snapshot factory/mapper.
5. KZ-18 group-deletion planning split.
6. KZ-19 reference-data failure model.

### Wave 3: responsive UI and style ownership

1. KZ-07 desktop/mobile convergence.
2. KZ-13 SCSS ownership classification and menu primitive.
3. KZ-14 dialog options/confirmation shell.
4. KZ-10 inventory page shell/state cleanup.
5. KZ-24 stale interface metadata cleanup integration.

### Wave 4: low-risk consolidation

1. KZ-11 error mapping ergonomics.
2. KZ-15 catalog state/`Version` cleanup.
3. KZ-16 filter micro-clones.
4. KZ-20 HTTP registration/client boilerplate.
5. KZ-21 public surface cleanup.
6. KZ-22 opportunistic presentation cleanup.
7. KZ-23 consistent use of `SimpleUriBuilder` and `SimpleUriParser`.

## Suggested blitz exit criteria

The blitz is complete when:

- Razor pages no longer contain transport DTO construction or direct rule API access;
- client filtering no longer implements its own CIDR/port parser or set algebra and instead consumes the shared semantic domain introduced by PR #40;
- desktop/mobile rule presentations share their interaction behavior and semantic content implementation;
- component styling follows the documented isolated/global ownership rule with explicit exceptions only;
- known/tag/group limits and validator identities are stable shared contracts rather than UI magic values/string matching;
- all affected client unit tests pass, plus client build/publish validation for Sass/scoped CSS changes;
- no new generic “framework” abstractions are introduced merely to eliminate a few lines of obvious domain-specific code.
