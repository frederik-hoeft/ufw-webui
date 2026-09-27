# Rule templates and reversible disable implementation plan

This document is the active implementation plan for rule templates and the reversible disable workflow. It refines the template backlog item in [backlog.md](backlog.md) against the current implementation and records the intended review boundaries for the work.

The plan is intentionally temporary. Once the implementation is approved, its durable contracts belong in the permanent architecture and development documentation; this file and the completed backlog item should then be removed.

## Goal

Add ASP-owned reusable rule templates that capture an authoring definition without becoming firewall state or firewall identity.

The feature should let an administrator:

- create, edit, and delete templates without any UFW side effects;
- save one parsed live rule occurrence as a template;
- start ordinary rule creation from a template;
- disable a live rule by persisting its reusable definition first and then performing the existing signed delete;
- re-enable a disabled rule by loading its template into the ordinary create/sign/mutate flow.

The central ownership boundary remains:

```text
PostgreSQL template
    |
    | copy into an independent draft
    v
FirewallRuleSpecification + authoring metadata
    |
    | validate / edit / sign
    v
ordinary UFW mutation
```

A template is never authoritative for live firewall state. Creating a rule from a template does not create a durable binding between the template and the resulting rule, and editing or deleting a template never mutates UFW. Templates intentionally do not retain live-ruleset placement: a template can initialize either ordinary append creation or an existing snapshot-bound ordered-insertion workflow, but the target position always comes from the live creation context.

## Current-state inventory

### Reusable browser authoring primitives

The rule-authoring UI already has the major reusable pieces this feature needs:

- `RuleEditor` owns the complete structural rule form, canonical UFW preview, shared structural validation, known-host/interface suggestions, and optional mutation-authorization input. `ShowAuthorization=false` already permits use as an authorization-free authoring surface.
- `IRuleDraftFactory` / `RuleDraftFactory` can create default drafts or normalized independent copies of existing `FirewallRuleSpecification` instances.
- `RuleMetadataEditor` and `RuleMetadataEditorResult` own notes, tag selection, and group selection independently of firewall semantics.
- `IRuleTagCatalogService` and `IRuleGroupCatalogService` provide the reusable ASP-owned metadata catalogs needed by the metadata editor.
- `CreateRule` already composes a structural draft and metadata draft, validates both, performs the ordinary signed add/ordered-insert operation, and then saves metadata against the daemon-confirmed rule identity.

A template manager therefore should not introduce a second rule form or template-specific structural validation. Template create/edit pages can reuse `RuleEditor` with authorization hidden and can reuse the existing metadata editor if template metadata is included in the agreed schema.

`RuleEditorMode.Edit` is specifically live-rule replacement presentation and locks address family. Template editing is different: templates are independent authoring artifacts, so the manager can use create-style structural editing with custom save labels rather than overloading live-rule edit mode.

### ASP persistence and catalog patterns

ASP already owns several reusable PostgreSQL-backed catalogs:

- known hosts demonstrate stable UUIDv7 public identity, normalized uniqueness where aliases require case-insensitive lookup, repository/service separation, typed REST inventory/mutation contracts, and a dedicated browser inventory manager;
- rule tags and groups demonstrate reusable metadata catalogs with stable UUIDv7 identity and referential integrity from rule metadata;
- `RuleMetadataRepository` demonstrates transactional validation of referenced tag/group identities and one-save-unit persistence for metadata graphs.

Rule templates fit the same ownership model: an internal EF entity and repository/service pair behind an authenticated `/api/v1/rule-templates` REST resource, with pure DTOs in `Ufw.Web.Model` and a typed browser API/catalog service.

The template repository should persist structural rule fields explicitly rather than serializing an opaque JSON copy of `FirewallRuleSpecification`. The rule model is small and stable enough that explicit columns keep schema constraints, migrations, reviewability, and future field changes visible. Mapping to/from `FirewallRuleSpecification` should be centralized so normalization is not duplicated across repository/controller code.

### Metadata persistence and reuse boundary

Live rule metadata currently consists of notes, stable tag identities, and at most one stable group identity. It is keyed by semantic `RuleId`, which is appropriate for live rules but inappropriate for templates because a template is not a live semantic firewall identity.

If templates preserve application metadata, they need their own template-owned metadata relationships rather than a fake `RuleId` or an orphaned `RuleMetadataEntry`:

```text
RuleTemplateEntry
  - structural FirewallRuleSpecification fields
  - notes
  - optional RuleGroupEntry FK
  - RuleTemplateTagEntry[] -> RuleTagEntry
```

This naturally preserves current tag/group renames because templates reference stable UUID-backed database identities. It also means template references must count as real tag/group usage:

- a referenced tag cannot be deleted until templates no longer reference it;
- a referenced group cannot be deleted until templates no longer reference it;
- the group-deletion workflow must know about template references before it performs any firewall batch deletion, otherwise it could delete live rules and only afterward discover that the ASP group is still retained by a template.

The existing metadata normalization currently lives privately inside `RuleMetadataService`. If template metadata uses the same notes/tag-count/group-ID constraints, Phase 1 should extract one narrow ASP-owned metadata-values normalizer/factory rather than copy those rules into a second service.

### Rule-list actions and mutation workflows

`RuleActionsMenu` already separates ordering, insertion, structural edit, metadata edit, and delete. Templates introduce two conceptually different actions:

- **Save as template** is ASP-only. It can copy any parsed occurrence that has a usable structural rule, including a duplicate semantic occurrence, because no firewall target is mutated.
- **Disable rule** is a composed firewall workflow. It inherits the existing single-rule delete safety boundary and therefore requires a uniquely deletable semantic identity (`CanMutate`) even if saving that row as a template by itself would be safe.

The existing `IRuleMutationService.DeleteRuleAsync` remains the firewall operation used by disable. No new daemon route, signed-intent operation, or UFW command is required.

The safe disable ordering from the backlog remains correct:

1. persist the template successfully;
2. issue the ordinary signed delete for the live rule;
3. if delete fails or its result is uncertain, retain the template and report that the live firewall rule may still be active;
4. if delete succeeds, let the existing in-band delete metadata cleanup run normally.

This ordering deliberately prefers an extra ASP template over losing the reusable definition.

### Template-manager UX precedent

`KnownHostsPage` and `RuleGroupsManagement` provide the closest inventory-management precedents, but a rule template is larger than either resource because its editor is the full rule authoring form. A page-sized template editor is therefore preferable to forcing `RuleEditor` into a large modal.

A maintainable browser shape is:

```text
/templates
  inventory / actions / use-template entry point

/templates/create
  name + description
  RuleEditor (authorization hidden)
  RuleMetadataEditor

/templates/edit/{id}
  same reusable authoring surface, initialized from stored template
```

The primary navigation can expose **Templates** beside **Rules** / **Metadata** because templates are first-class management artifacts rather than a hidden rule-page mode.

Saving a live rule as a template does not require a page-level snapshot-navigation contract. The rules page already holds the exact reviewed structural occurrence and joined metadata. A focused dialog can collect the template name/description and persist a copy of that reviewed data. If the live rule changes out of band after the snapshot was loaded, saving the reviewed definition is still safe because no UFW mutation occurs.

Disable is different because it performs a firewall mutation. The workflow should use the currently authoritative rule-page snapshot and the same uniqueness/mutation checks as delete before it is offered. The dialog should capture the exact rule definition being disabled rather than allowing the user to edit the template into a different structural rule during the same operation. Users who want a modified derivative can use **Save as template** and edit the template separately.

### Authentication, ownership, and audit state

The application supports multiple authenticated ASP.NET Identity users, but firewall rules, known hosts, tags, groups, and metadata are currently application-global rather than per-user resources. There is also no general audit/accountability subsystem.

Introducing per-user template ownership or isolated template-only audit fields would create a new authorization/accountability model that does not exist for neighboring management artifacts. The proposed first version therefore treats templates as a shared application-global catalog. If user ownership or auditing becomes a product requirement, it should be designed consistently across the management plane rather than introduced only for templates.

### Reconstructed development baseline

The approved rule-editing work has been reconstructed deterministically from the supplied 2026-09-27 source archive plus every approved incremental patch through the steady-state documentation phase. The supplied .NET SDK 10.0.400 and 168-package offline NuGet archive have been re-established in an isolated tooling directory, and the full solution restores successfully with package sources disabled.

The previous approved feature's final regression established 1,210/1,210 tests passing. This planning branch changes documentation only; each implementation phase below will rerun the test surfaces it affects, and the final documentation phase will repeat the strongest practical full regression.

## Proposed template contract

### Identity and naming

Each template should have:

- a stable UUIDv7 public identity independent of its display name;
- a required human-facing name;
- a case-insensitive normalized-name key with a database uniqueness constraint;
- an optional human-facing description that is template metadata, not a UFW comment.

Renaming a template preserves its UUID. Name conflicts return a typed `409 Conflict` result and never overwrite another template implicitly.

The template identity is never copied into a live firewall rule or signed mutation. There is no template-to-rule provenance link in the initial implementation.

### Structural rule payload

A template stores a normalized `FirewallRuleSpecification` including its ordinary UFW `Comment` field. It may store `FirewallAddressFamily.Any` because templates are authoring artifacts rather than observed live rules.

Template persistence validates the shared structural syntax using `RuleSpecificationNormalizer` and `RuleSpecificationValidator`, but it should not validate current daemon capabilities or host-interface existence. A template may therefore remain useful while IPv6 is currently disabled or while an interface is temporarily absent. Current environment/capability checks still apply when the template is loaded into ordinary rule creation and submitted to the daemon.

This keeps the contract clean:

- persistence answers “is this a valid reusable rule definition?”;
- rule creation answers “is this definition currently executable on this firewall?”

### Authoring metadata

Templates preserve the complete reusable authoring metadata draft: notes, tag identities, and optional group identity.

This is the most coherent interpretation of reversible disable. Re-enabling a disabled rule can restore the same authoring context through the existing create workflow instead of silently discarding notes/tags/group membership when the live rule is deleted.

Template metadata is an independent snapshot. Creating a rule from a template copies the current template values into the create-page draft; later template edits do not affect that draft or the resulting rule.

Because tag/group references are stable identities rather than copied display strings, later tag/group renames naturally appear when the template is opened again. Referential integrity prevents a template from silently degrading because one of its reusable metadata dependencies was deleted.

### REST shape

Add an authenticated ASP resource under `/api/v1/rule-templates` with ordinary catalog semantics:

- `GET /rule-templates` -> complete sorted inventory;
- `POST /rule-templates` -> create;
- `PUT /rule-templates/{id}` -> update;
- `DELETE /rule-templates/{id}` -> delete.

A separate per-id GET is optional if the browser catalog service can resolve edit/create-from-template navigation from the inventory efficiently; it should be added only if it materially simplifies direct deep-link loading rather than by default.

Create/update requests carry template name/description, the complete structural rule, and template metadata values. Mutation responses return the resulting inventory, matching existing known-host/tag/group catalog behavior and keeping browser caches straightforward.

The ASP service owns normalization and validation. Controllers map typed service outcomes to HTTP and do not contain EF logic.

## User workflows

### Create/edit/delete template

Template create/edit is purely ASP-owned:

1. initialize an independent structural draft plus metadata draft;
2. validate the structural rule with shared authoring validation and validate template name/description/metadata;
3. persist the complete template transactionally;
4. return the updated template inventory.

No private key is requested and no daemon endpoint is called.

Deleting a template deletes only the template. It never deletes a live rule that may previously have been created from that template because no persistent binding exists.

### Save live rule as template

The rule action menu exposes **Save as template** for parsed rules that can be represented as `FirewallRuleSpecification`, independently of whether their semantic identity is unique.

The action uses the exact reviewed row plus its joined ASP metadata as the initial template contents. A focused dialog collects template name/description and saves that snapshot. Duplicate semantic live rules are naturally supported because this workflow performs no firewall mutation and the row occurrence already supplies the concrete structural rule/comment being copied.

### Create/re-enable from template

Template loading is an initialization action inside the ordinary `CreateRule` workflow rather than a separate mutation mode. Both entry paths are first-class:

- **Add rule** opens ordinary append creation; the user can then load a template into the creation draft.
- **Insert before/after** opens the existing snapshot-bound ordered-insertion workflow; the user can then load a template into that position-constrained creation draft.

The template manager may additionally expose **Create rule** / **Use template** as a convenience deep link that opens normal creation with the selected template already loaded. This shortcut must converge on exactly the same creation-page state and validation as choosing the template after arriving through **Add rule**.

Template values are only initialization. Loading a template clones its structural definition and metadata into independent drafts; the user may then change them before signing. The eventual add/insert request remains the ordinary current `FirewallRuleSpecification` mutation and contains no template identity.

Live-ruleset placement is never part of the template. Initialization precedence is therefore explicit:

1. an ordered-insertion context, if present, remains authoritative for target family and position;
2. loading a template initializes only reusable structural/metadata values and must reject a template whose concrete family conflicts with the ordered-insertion family rather than changing the insertion target;
3. for ordinary append creation, a loaded template may initialize its own family, including `Any`;
4. the existing `family=` convenience parameter applies only until a template is explicitly loaded and may not contradict an ordered-insertion context.

A missing/deleted template produces an explicit template-context warning and leaves the existing create context intact. It must not silently clear or replace the current draft while presenting the load as successful.

### Disable rule

**Disable rule** is offered only where ordinary single-rule deletion is already safe.

The confirmation workflow shows the exact rule being disabled, captures template name/description and mutation authorization, and then:

1. creates the template from the reviewed structural rule and metadata;
2. only after template persistence succeeds, signs and issues the existing rule delete;
3. on confirmed delete success, refreshes authoritative rule state through the existing inventory path;
4. on delete rejection/failure/uncertainty, keeps the template and reports the firewall outcome without trying to “roll back” template creation.

No cleanup should automatically delete the template after a failed firewall delete. The persisted reusable definition is the intentional safety artifact.

A retry after such a partial workflow does not require hidden idempotency machinery in the first version. The UI can tell the administrator that the template already exists and the rule remains active; they may retry ordinary deletion or manage the saved template explicitly. If exact same-template retry support follows naturally from the implementation, it can be added without changing the safety contract.

## Phased implementation

Each phase starts from the latest approved baseline on its own branch, is validated, exported as an incremental patch against the last review-visible state, and remains unmerged until explicitly approved. Tests ship with the behavior they cover.

### Phase 1: template persistence and REST foundation

Branch: `feature/rule-template-persistence`

Scope:

- add `RuleTemplateEntry` plus explicit EF mapping and a migration;
- add template-tag relation persistence and optional template-group relation if the metadata-scope decision below is approved;
- add `Ufw.Web.Model.V1.RuleTemplates` inventory/item/create/update contracts;
- add focused template specification <-> persistence mapping;
- extract one narrow reusable ASP metadata-values normalization primitive if notes/tags/group constraints would otherwise be duplicated;
- add `IRuleTemplateRepository` / `RuleTemplateRepository` and `IRuleTemplateService` / `RuleTemplateService`;
- validate/normalize names, descriptions, structural rule syntax, tag/group references, and atomic create/update behavior;
- add authenticated `/api/v1/rule-templates` controller routes and explicit conflict/not-found/validation responses;
- extend tag/group deletion usage checks so template references cannot be orphaned;
- if templates may reference groups, extend the group inventory contract/projection with enough template-reference information for the browser to reject group deletion before any live-rule batch mutation begins;
- register services and source-generated REST serialization metadata;
- add repository/service/controller tests, including EF transaction/failure coverage and migration/model validation.

QA gate:

- targeted `Ufw.Web.Model`, `Ufw.Web`, and `Ufw.Web.Tests` builds/tests pass offline;
- relevant PostgreSQL/WKG integration tests cover create/update/delete, case-insensitive name conflicts, missing metadata dependencies, and transactional rollback;
- existing tag/group deletion tests cover template references;
- review confirms no daemon/shared signed-intent changes and no serialized firewall-authority state in PostgreSQL.

### Phase 2: template catalog and manager UX

Branch: `feature/rule-template-management`

Scope:

- add typed browser API client and source-generated client serialization for rule templates;
- add a focused template catalog service/domain projection rather than letting pages interpret raw REST DTOs ad hoc;
- add **Templates** to primary navigation;
- add `/templates` inventory with responsive desktop/mobile presentation and create/edit/delete actions;
- add page-sized `/templates/create` and `/templates/edit/{id}` authoring surfaces;
- reuse `RuleEditor` with mutation authorization hidden and reuse `RuleMetadataEditor` if metadata is part of the template contract;
- keep template editing independent of current UFW snapshots while still using known-host/interface catalogs as authoring suggestions;
- add canonical-command preview, name/description validation, delete confirmation, localization, and accessibility states;
- add browser unit/component-state tests for catalog protocol validation, draft independence, create/update/delete error handling, and current-capability-independent template editing.

QA gate:

- full `Ufw.Web.Client.Tests` and targeted Web Client build pass offline;
- no private-key UI or firewall API call is reachable from template create/edit/delete;
- manual review confirms the full rule editor is not duplicated or squeezed into an unsuitable modal;
- `git diff --check` passes.

### Phase 3: live-rule snapshotting and create-from-template

Branch: `feature/rule-template-authoring-flow`

Scope:

- add explicit row capability/action plumbing for **Save as template** without inheriting single-delete duplicate restrictions;
- add a focused save-as-template dialog that copies the exact reviewed structural occurrence plus joined metadata and collects template name/description;
- add template loading/picking to `CreateRule`, so the same workflow supports **Add rule -> load template** and **Insert before/after -> load template**;
- add **Create rule** / **Use template** actions to the template manager as convenience deep links into that same creation-page initialization path;
- add `template=<uuid>` preselection/loading support and a small feature service/value mapper if needed to keep template DTO interpretation out of page code;
- define and test template/family/ordered-insertion initialization precedence without weakening existing snapshot-bound insertion semantics or copying positional state into templates;
- clone all template values into independent drafts so later edits or template updates cannot mutate each other;
- preserve ordinary create/ordered-insert signing, daemon validation, metadata-save, and authoritative refresh behavior unchanged after initialization;
- add rule-list, template-manager, create-page, and localization tests for duplicate source occurrences, missing/deleted templates, IPv6-disabled current hosts, metadata prefill, and draft independence.

QA gate:

- full client tests and targeted hosting Web build pass offline;
- save-as-template causes no daemon/firewall call;
- create-from-template emits exactly the same signed add/insert contract as manually authored creation;
- review confirms no template identity/provenance leaks into UFW or signed intent.

### Phase 4: reversible disable workflow

Branch: `feature/rule-template-disable`

Scope:

- add **Disable rule** beside ordinary delete, gated by the existing safe single-rule mutation capability;
- add a dedicated disable confirmation UI that shows the exact structural rule, captures template name/description, and obtains the existing mutation private key without permitting structural edits inside the composed operation;
- add a focused browser workflow service/result model that persists the template first and calls the existing `IRuleMutationService.DeleteRuleAsync` only after persistence succeeds;
- retain the template for every non-successful/uncertain delete outcome;
- reuse the existing rule inventory stale/refresh transitions after delete success or uncertain transport outcomes rather than inventing optimistic local firewall state;
- let existing ASP in-band delete metadata cleanup run after confirmed deletion; do not create a special daemon operation or hidden metadata tombstone;
- ensure group-cleanup options and template preservation compose predictably if ordinary delete currently offers orphan-group cleanup;
- add tests for template-create failure/no UFW call, delete success, delete rejection, transport uncertainty, duplicate/unmutable source rejection, metadata/template preservation, and retry-facing diagnostics.

QA gate:

- full client tests plus affected Web/Systemd/IPC regression surfaces pass offline;
- integration coverage proves the daemon receives the ordinary existing signed delete operation, not a new disable command;
- review confirms there is no path that deletes the live rule before durable template persistence succeeds;
- `git diff --check` passes.

### Phase 5: steady-state documentation reconciliation and final regression

Branch: `docs/rule-templates-steady-state`

Use `dev/technical-documentation.skill` for this phase.

Scope:

- reconcile permanent documentation against the approved implementation rather than retaining implementation-history prose;
- update at minimum:
  - `README.md` capability summary;
  - `docs/architecture/firewall-model.md` for the template/live-rule ownership boundary and reversible-disable semantics;
  - `docs/architecture/browser-application.md` for template catalog/authoring and composed disable workflow responsibilities;
  - `docs/architecture/security.md` only where it clarifies that templates are authenticated ASP state while instantiation/deletion still requires ordinary daemon-signed mutations;
  - `docs/architecture/architecture-overview.md` for PostgreSQL template ownership and process boundaries;
  - `docs/development/client-ui.md` only if reusable authoring/page conventions materially change;
- search for stale statements about tag/group usage, rule metadata, or available rule actions that the template feature changes;
- remove the completed template backlog section and delete this temporary plan once durable behavior is represented in permanent docs;
- run the strongest practical full offline build/test regression plus Markdown link/coherence checks.

QA gate:

- permanent docs describe steady-state template semantics and agree with implementation;
- no completed template planning residue remains under `docs/internal`;
- all relevant test projects/builds supported by the environment pass;
- final branch remains documentation-only apart from strictly necessary documentation tooling fixes.

## Settled design decisions

The planning gate established the following contracts for Phase 1 and later phases:

1. **Complete reusable metadata, no live placement.** Templates preserve structural UFW fields plus notes, tags, and optional group membership. They never store live ruleset occurrence/position information. Position always comes from the ordinary append or snapshot-bound ordered-insertion context in which a template is instantiated.
2. **Global catalog.** Templates are application-global like rules, hosts, tags, groups, and metadata. There is no per-user ownership state.
3. **Context-free persistence validation.** Template create/update validates normalized structural correctness and ASP metadata references only. It deliberately ignores current ruleset facts such as semantic duplicates, IPv6 enablement, and current interface availability. Those checks remain part of ordinary rule instantiation/mutation.
4. **Stable identity without provenance.** Templates use UUIDv7 identity, a case-insensitive unique display name, an optional description, and no persistent template-to-live-rule provenance. Instantiation copies values into an independent draft and forgets the template identity.
5. **Disable snapshots exactly, then deletes.** Disable persists a direct snapshot of the reviewed live rule plus its metadata and user-supplied template name/description, then performs the existing signed delete. It never offers structural template customization inside the composed disable workflow. Template customization remains the normal template-management flow.

These decisions intentionally keep templates reusable and context-free while leaving all live firewall authority, placement, duplicate detection, capability checks, and mutation safety in the existing creation/deletion workflows.
