# Rule presentation and enrichment sprint baseline

This document captures the agreed architectural direction for the next rule-presentation sprint. Here, **sprint** is only a convenient name for a coherent body of work: the plan is organized into work-based phases and sub-phases rather than a time-boxed schedule with deadlines. It is temporary maintainer guidance rather than steady-state architecture documentation; exact database entities, REST resources, filter catalogues, query syntax, and UI details should be designed against these invariants and moved into permanent documentation when implemented.

The sprint extends the current authoritative UFW rule view with application-owned metadata, filtering/search, richer row presentation, and grouping without turning PostgreSQL into a second firewall database. Reusable rule templates and other larger follow-on capabilities are tracked separately in the [long-term feature backlog](long-term-feature-backlog.md).

## Authority and data flow

UFW remains the only authority for live firewall rules. `Ufw.Web` must not mirror the current UFW rule set into PostgreSQL or infer that a database record represents an active firewall rule.

The browser-facing read path should treat ASP as an enriching/transforming proxy:

```text
Ufw.Systemd
  authoritative UFW snapshot
        |
        v
Ufw.Web
  join application state by semantic rule identity
  + attach ASP-owned presentation metadata
  + expose one enriched read model
        |
        v
Ufw.Client
  retain the current enriched snapshot
  + apply local presentation/query state
        |
        v
visible rule list
```

A normal list/read operation must remain side-effect free. ASP may lazily create persistent entities when an administrator actually attaches application-owned state to a rule, but merely observing or enriching a rule must not create, reconcile, or delete database state.

ASP-owned state is application context only. It must never become part of the signed firewall rule, daemon authorization contract, or authoritative rule identity.

## Semantic identity and enrichment

Rule enrichment is keyed by the existing opaque semantic `RuleId` produced from normalized firewall semantics. ASP treats this value as an opaque join key and does not reproduce firewall identity rules independently.

A metadata record therefore describes the semantic rule, not one UFW list occurrence. If multiple indistinguishable UFW occurrences share the same semantic identity, they intentionally receive the same ASP metadata. Snapshot-local occurrence IDs remain suitable for ordering/anchor semantics only and must not be persisted as application identity.

The enriched browser model should preserve the distinction between:

- authoritative UFW state and capabilities;
- authoritative structural rule data and semantic identity;
- ASP-owned presentation metadata such as tags, groups, notes, or other contextual fields;
- snapshot-local presentation/ordering coordinates.

The details surface should be extensible, but known metadata concepts should use typed contracts rather than an unstructured `Dictionary<string, object>` style property bag. Extensibility belongs at the presentation/contract boundary, not in Razor-specific ad hoc data structures.

## Metadata lifecycle and reconciliation

Application metadata follows the live semantic rule by identity but is not itself evidence that the rule exists.

### In-band deletion

When UFWeb successfully deletes a live rule through its own mutation flow, ASP can immediately remove the metadata attached to that semantic identity. The application has authoritative knowledge that the in-band operation completed and can clean up the corresponding enrichment state as part of that application workflow.

### Out-of-band deletion

When a rule disappears because UFW was changed outside UFWeb, unmatched metadata is retained rather than deleted during the next rules read. It simply has no authoritative rule to enrich and therefore does not appear in the normal rule UI.

This state is best described as **unmatched** or **orphaned metadata**, not soft deletion. No live firewall object is represented by the database row.

If an equivalent semantic rule is recreated before the metadata is garbage-collected, the retained metadata naturally reattaches. This gives out-of-band deletion a limited reversible quality without changing firewall authority.

### Explicit reconciliation

Orphan cleanup is explicit. A reconciliation operation can compare current authoritative semantic identities with stored rule metadata and report state such as:

> 7 metadata records no longer correspond to active firewall rules.

The operator can then remove unmatched records deliberately. The project already uses explicit reconciliation for other host-derived application state, so this is a familiar operational concept.

No age-based retention policy or orphan timestamp is required for the initial implementation. Those can be added later if stale metadata volume becomes operationally relevant.

The initial enrichment infrastructure stores typed optional group/notes fields plus case-insensitive tags under the semantic `RuleId`. `GET /api/v1/rules` now returns the daemon `RuleListResponse` as an authoritative sub-model alongside only metadata matching identities in that snapshot. Metadata writes verify that the semantic identity is currently live before persisting, and a successful in-band delete performs best-effort metadata cleanup after the firewall mutation is confirmed. Explicit orphan discovery/removal remains a later reconciliation slice.

## Family-local presentation and ordering

IPv4 and IPv6 are independently ordered UFW rule sets. Their concatenated `ufw status numbered` coordinates remain important to daemon/protocol compatibility, but cross-family relative position has no packet-processing meaning.

The browser therefore uses family-relative presentation coordinates:

- the visible `Position` is one-based within the selected address family;
- the authoritative combined UFW display number remains part of the underlying snapshot for protocol/subprocess semantics only;
- ordering previews, move targets, position-change indicators, and ordering-result messages use family-local coordinates;
- snapshot-local occurrence identity remains distinct from user-facing family position;
- reordering is limited to one address family.

The shared `/rules` page presents IPv4 and IPv6 as tab-selected `RuleFamilyWorkspace` instances. Global firewall state, refresh/mutation orchestration, and family selection stay at page level; drag/drop and move interaction remain family-local. The component boundary is independent of the tab layout, so a later navigation change would not alter rule authority or ordering semantics.

## Rule-list projection

Rule identity, duplicate handling, mutation capabilities, family positions, and ordering state must be derived from the complete authoritative snapshot before any filtering is applied.

The intended pipeline is:

```text
authoritative UFW snapshot
        +
ASP enrichment by semantic identity
        |
        v
canonical rule-list projection
  - authoritative rule state
  - semantic identity
  - snapshot occurrence identity
  - family-local position
  - mutation/ordering capabilities
  - application metadata
        |
        + optional ordering preview
        |
        v
presentation rows
        |
        + client query/filter state
        |
        v
visible rows + structured match context
```

Filtering must never renumber the authoritative projection, change duplicate detection, alter occurrence identity, or redefine mutation targets.

## Search and filtering

The baseline direction is to perform search and filtering in the browser over the current enriched snapshot.

Expected rule counts are small enough that transferring the complete enriched snapshot and evaluating queries locally is simpler than turning every query update into another daemon/ASP round trip. This also avoids introducing ASP-side cache/freshness semantics merely to make server-side search efficient.

ASP remains responsible for assembling truth; the client owns view projection.

### Structured query model

Filtering should be modelled as a collection/pipeline of independently configured filters rather than one monolithic options object that knows every supported predicate.
In the initial client implementation, free-text search is also represented by a configured `TextFilter` so every active constraint follows the same evaluator and evidence pipeline.

Conceptually:

```text
RuleQuery
  configured filters*
    TextFilter
    SourceFilter
    DestinationFilter
    PortFilter
    ProtocolFilter
    ActionFilter
    DirectionFilter
    TagFilter
    GroupFilter
    ...
```

The initial combination rule should be simple conjunction: every configured filter must pass for a row to remain visible. More expressive boolean query trees are intentionally deferred until a concrete use case justifies the added UI and evaluation complexity.

Each configured filter is an instance with its own data model. Filter semantics should live in focused evaluator logic rather than in Razor components or one central `RuleFilterOptions` class. The exact interface names are implementation details, but the responsibility split should remain approximately:

```text
filter data model
      |
      v
filter evaluator
  row + family/query context
      |
      v
pass/fail + structured match evidence
```

A filter evaluator must not emit HTML, `RenderFragment`, Blazor component types, or other presentation objects.

The selected IP-family workspace is evaluation context rather than another filter. IPv4 filtering operates over the IPv4 projection and IPv6 filtering over the IPv6 projection. Family-sensitive parsing and network predicates can therefore specialize cleanly without adding `family=v4` as an ordinary user-visible filter inside a family-local workspace.

Sorting is also separate from filtering. Filters determine which rows are visible; sorting determines how the resulting view is presented. Firewall-order sorting remains the mode in which reordering semantics are meaningful.

### Structured match evidence

Search/filter evaluation should return visible rows together with typed, semantic evidence explaining why a row matched. This evidence is accumulated by the configured filters that a surviving row passes through.

Examples include:

```text
TagMatch
  tag = observability

NetworkMatch
  field = Source
  rule network = 10.100.20.0/24
  query network = 10.100.20.17
  relationship = Contains

TextMatch
  field = Comment
  range = [12, 22)
```

The concrete evidence types are implementation details, but they should describe domain/query semantics rather than rendering instructions. Desktop and mobile presenters can then render the same evidence safely and consistently.

This extends the existing rule-list projection without changing authority:

```text
canonical presentation row
        |
        + RuleQuery
        |
        v
visible row
  + RuleMatchEvidence*
```

A row that fails one filter can be discarded immediately. A row that survives the complete filter pipeline retains the accumulated evidence needed for match-context presentation.

### Filter UI direction

The long-term UI should treat filters as composable objects rather than expose every possible field permanently.

Applied filters are represented compactly as removable chips/tags, for example:

```text
[ Source: 10.0.0.0/8 × ] [ Protocol: TCP × ] [ Tag: observability × ]
```

Removing a chip removes that filter instance. Selecting an existing chip should be able to reopen its editor with the current filter data pre-filled.

The eventual dynamic filter-builder direction is:

```text
+ Filter
   |
   v
filter selector / autocomplete
   |
   v
selected filter editor
  filter-specific input fields
   |
   v
configured filter instance
   |
   v
applied-filter chip
```

The available-filter catalogue and configured-filter instances are distinct concepts. A filter definition/registration can provide a stable key, display name/category, editor registration, and presentation metadata, while a configured filter instance contains only the values needed by that filter's semantics.

Blazor can host a filter-specific editor dynamically, but dynamic component types belong in client-side UI registration rather than the filter/evaluator domain model. Most filter chips and match evidence should use shared generic presenters fed by small presentation models; bespoke components should be reserved for cases that genuinely need them.

The editor host does not need to be fixed to a modal dialog. A desktop implementation may use a compact popover anchored to `+ Filter` or an existing chip, while a narrow/mobile layout may present the same editor component inside a dialog or sheet.

### Phased filtering implementation

The filtering feature should evolve in work-based phases so that the architectural work remains useful even before the dynamic UI exists.

#### Phase 1: structured baseline

Start with a straightforward, hard-coded structural filter form and modular code behind it.

The first slice should establish:

- configured filter models rather than one monolithic filter state object;
- focused filter evaluators/pipeline composition;
- typed match evidence;
- free-text search represented as a normal configured filter that can coexist with structural filters;
- applied-filter chip presentation where practical;
- family-local evaluation context;
- query/filter state separated from sorting and ordering state;
- tests for filter composition, CIDR-aware semantics, text matching, evidence generation, and interaction-mode invariants.

The hard-coded form is deliberately replaceable UI, but the query/evaluator/evidence model is not throwaway work.

#### Phase 2: dynamic filter composition

When the fixed form becomes crowded, replace it with the filter-selector/editor flow:

- `+ Filter` opens a searchable/category-aware filter selector;
- selecting a filter dynamically renders that filter's editor;
- applying the editor adds one configured filter instance;
- clicking an applied-filter chip reopens that editor with existing values;
- removing a chip removes only that filter instance;
- new filter types can be added primarily through registration plus their model/evaluator/editor/presentation pieces rather than by expanding a central form.

This phase should preserve the same query model and evaluator pipeline introduced in phase 1.

The implemented client shape keeps the dedicated free-text field as a convenience entry point for the same `TextRuleFilter` model while structural filters are composed through `+ Filter`. UI registrations are supplied per filter domain and aggregated by a generic catalogue; each registration owns its stable key, category/name metadata, editor component, configured-filter matching, and chip presentation. The current editor host is a dialog, but the editors and catalogue are independent of that host so a later popover/sheet presentation does not change filter semantics.

#### Phase 3: optional shorthand query grammar

A compact context-search grammar can be added later as a power-user convenience, for example:

```text
from:10.0.0.0/8 to:10.1.2.3 proto:tcp tag:observability "prometheus"
```

The grammar must compile into the same configured filter models used by the structural UI. Unqualified text should compile to the same text-filter model rather than become a second
filtering implementation.

Unqualified text should remain ordinary text search rather than being aggressively inferred as an address, port, tag, or other structured predicate. Explicit prefixes can add precision without making normal search surprising.

The shorthand grammar is optional. It should only be implemented if the structural UI and real usage demonstrate that the convenience is worth the grammar, completion, validation, and discoverability work.

### Navigation and refresh behavior

Client-side evaluation does not preclude navigable query URLs. Query state can be parsed from and synchronized to the rules route. The exact encoding is deferred because the filter collection may outgrow a fixed set of scalar query parameters.

Reloading or browser navigation fetches the current enriched snapshot and reapplies the current query. A refresh atomically replaces the loaded snapshot while preserving/reapplying active query state.

A dedicated server-side `/rules/search` endpoint is not part of the baseline. It should only be introduced later if a concrete server-side use case appears.

## Interaction modes

Filtering/search and rule reordering must not overlap.

At minimum the page has these interaction modes:

```text
Normal
  search/filter controls available
  reorder initiation available

Filtered
  one or more query/filter constraints active
  drag/drop and move/reorder controls disabled

Ordering preview
  staged ordering exists
  query/filter changes disabled until the preview is applied or discarded
```

The exact state type is an implementation detail, but the mutual exclusion is an invariant rather than a collection of per-control checks.

Ordered insertion is a separate signed mutation anchored to an exact snapshot occurrence. Its availability while filtering is active can be decided independently because it does not reorder existing visible rows.

## Row composition and expansion

A rendered rule has one stable summary plus two independent optional regions:

```text
Rule row/card
  Rule summary                 always visible
  Rule match context           query-derived, independently collapsible
  Rule metadata details        user-controlled, independently collapsible
```

The match section exists because of the current query. It may show information such as `Matched tag: observability` or a highlighted excerpt from notes, comments, canonical command text, group names, or other searchable metadata. It disappears when the corresponding query state disappears.

The metadata details section is explicit user interaction and may present richer ASP-owned context such as tags, group, notes, state, audit/context fields, canonical command text, or future typed metadata. Its expansion state is independent from search-match presentation.

These two regions must not share one generic `Expanded` flag or otherwise become coupled merely because both render beneath the summary.

## Component boundaries

The presentation foundation is already decomposed along interaction boundaries. The feature sprint should extend those boundaries rather than rebuilding family separation or collapsing the row presenters back into a monolith.

The current and planned component shape is approximately:

```text
Rules page
  global firewall state / mutation orchestration
  IPv4 | IPv6 tabs
    RuleFamilyWorkspace (selected family)
      RuleListToolbar                 planned
        free-text search
        applied filter chips
        filter editor/selector host
        sorting
      RuleDesktopRow*
        RuleMatchContext?             planned
        RuleMetadataDetails?          planned
      RuleMobileCard*
        RuleMatchContext?             planned
        RuleMetadataDetails?          planned
```

Responsibilities should remain narrow:

- the rules page owns authoritative refresh/mutation lifecycle, selected-family navigation, URL-backed query state, and page-level interaction mode;
- `IRuleListProjectionService` derives the canonical family partitions, occurrence identity, family positions, duplicate/mutation capabilities, and ordering state before query evaluation;
- `RuleFamilyWorkspace` owns one family's drag source/drop target, move interaction, family-local empty state, and the presentation surface for that family's future query controls/results;
- the query/filter layer owns configured filter state, evaluation, sorting state, and structured match evidence without depending on Razor rendering;
- the toolbar owns free-text entry, applied-filter chip interaction, filter-editor hosting, and sort selection, but delegates filter semantics to the query layer;
- desktop rows and mobile cards render one rule through their naturally different DOM structures and emit explicit callbacks for actions;
- match-context presentation renders structured query evidence;
- metadata-details presentation renders enriched rule context but does not perform persistence or daemon operations.

Avoid Razor component inheritance as the mechanism for IPv4/IPv6 specialization. Shared layout should use component composition, while family-sensitive parsing/validation/search behavior should be supplied through focused policies/services. This keeps Blazor lifecycle/render state out of inheritance hierarchies and allows most query predicates to remain family-independent.

High-frequency browser drag events should retain their current non-rendering treatment; future feature work must not introduce avoidable render churn during drag interaction.

## Existing presentation foundation

The presentation preparation is complete and is treated as the baseline for this sprint. In particular:

- authoritative `ListedFirewallRule.DisplayNumber` values are no longer rewritten for local ordering previews;
- browser-visible positions and ordering feedback are family-relative while daemon/protocol coordinates remain combined-snapshot values;
- rule-list projection terminology and services are rendering-agnostic;
- `/rules` owns tab-based IPv4/IPv6 family selection and composes one `RuleFamilyWorkspace` at a time;
- family-local drag/drop and move state are isolated from page-global refresh/mutation state;
- desktop rows and mobile cards are focused presenters rather than branches inside one monolithic table component;
- component-specific Sass follows those ownership boundaries.

The feature sprint should build on these contracts. Reworking family separation, numbering, ordering-preview authority, or the row/card decomposition is out of scope unless a concrete feature exposes a defect in the established foundation.

## Deferred detailed design

The following are intentionally not fixed by this baseline and should be worked through in the feature-design phase:

- exact EF entities, keys, relationships, indexes, concurrency fields, and migrations;
- exact browser-facing REST contract and whether enriched contracts warrant a dedicated API-contract project;
- concrete metadata fields and grouping semantics;
- exact future shorthand query grammar, URL encoding, filter-editor hosting surface, filter catalogue, and highlighting/presentation polish;
- whether ordered insertion remains available while a filter is active;
- reconciliation endpoint/command shape and orphan-cleanup confirmation UX;
- optional direct rule-detail navigation;
- any future automated orphan-retention policy.

These decisions must preserve the authority, identity, reconciliation, and interaction invariants above.
