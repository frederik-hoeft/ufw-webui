# Rule presentation and enrichment sprint baseline

This document captures the agreed architectural direction for the next rule-presentation sprint. It is temporary maintainer guidance rather than steady-state architecture documentation: exact database entities, REST resources, query syntax, and UI details should be designed against these invariants and moved into permanent documentation when implemented.

The sprint extends the current authoritative UFW rule view with application-owned metadata, filtering/search, richer row presentation, grouping, and reusable rule templates without turning PostgreSQL into a second firewall database.

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

## Family-local presentation and ordering

IPv4 and IPv6 are independently ordered UFW rule sets. Their concatenated `ufw status numbered` coordinates remain important to daemon/protocol compatibility, but cross-family relative position has no packet-processing meaning.

The UI should therefore move toward family-relative presentation coordinates:

- render a family-local `#` or `Position` instead of presenting the combined UFW number as the primary user-facing ordinal;
- preserve the authoritative combined UFW display number in the underlying snapshot for protocol/subprocess semantics;
- compute ordering previews and position-change indicators in family-local coordinates;
- keep snapshot-local occurrence identity distinct from user-facing family position;
- permit only family-local reordering.

This makes IPv4 and IPv6 self-contained presentation units. They can remain stacked sections, become tabs, or move to separate routes later without changing rule authority or ordering semantics.

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

Search/filter implementation should still be separated from Razor rendering. A pure query evaluator should consume the current rule-list projection plus a structured query and return visible rows together with structured match information. Match results should identify semantic fields and text ranges rather than emitting HTML, allowing desktop and mobile renderers to highlight the same result safely.

Client-side evaluation does not preclude navigable query URLs. Query state can be parsed from and synchronized to the rules route, for example:

```text
/rules?q=ssh&family=v4&action=allow&tag=observability
```

Reloading or browser navigation fetches the current enriched snapshot and reapplies the URL query. A refresh atomically replaces the loaded snapshot while preserving/reapplying the active query.

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

The current rule table is long partly because it contains legitimate interaction complexity, but the enrichment/search work would add multiple independent axes of behavior. The useful split is by interaction responsibility, not by arbitrary markup size.

The intended component shape is approximately:

```text
Rules page
  RuleListToolbar
    search + filters

  RuleList / RuleTable shell
    RuleFamilyView (IPv4)
      RuleDesktopRow*
        RuleMatchContext?
        RuleMetadataDetails?
      RuleMobileCard*
        RuleMatchContext?
        RuleMetadataDetails?

    RuleFamilyView (IPv6)
      ...
```

Responsibilities should remain narrow:

- the rules page owns authoritative refresh/mutation lifecycle and page-level interaction mode;
- the list/table shell owns family composition and cross-family presentation only;
- `RuleFamilyView` owns one family's drag source/drop target, family-local ordering interactions, heading/count, and family-local empty state;
- desktop rows and mobile cards render one rule through their naturally different DOM structures and emit explicit callbacks for actions;
- match-context presentation renders structured query matches;
- metadata-details presentation renders enriched rule context but does not perform persistence or daemon operations.

Avoid Razor component inheritance as the mechanism for IPv4/IPv6 specialization. Shared layout should use component composition, while family-sensitive parsing/validation/search behavior should be supplied through focused policies/services. This keeps Blazor lifecycle/render state out of inheritance hierarchies and allows most query predicates to remain family-independent.

High-frequency browser drag events should retain their current non-rendering treatment; component extraction must not introduce avoidable render churn during drag interaction.

## Templates

Templates are ASP-owned authoring artifacts, not live rules and not part of live-rule identity.

The intended relationship is:

```text
Rule template
    |
    | load
    v
FirewallRuleSpecification draft
    |
    | edit / validate / sign
    v
UFW mutation
```

A template can pre-populate the existing rule editor, after which normal validation, signing, and daemon enforcement apply. Editing or deleting a template has no UFW side effects.

The live-rule action menu can offer **Save as template**. A separate template manager can create, edit, and delete templates without touching UFW.

### Disable rule

**Disable rule** is an application workflow built from existing concepts rather than a new daemon mutation:

1. persist a reusable template representation successfully;
2. issue the ordinary signed UFW delete;
3. if deletion fails, keep the template and report that the live rule remains active;
4. if deletion succeeds, clean up live-rule metadata according to the normal in-band deletion policy.

Persisting first is intentional. A failed delete can leave an extra template, while deleting first could lose the rule definition if template persistence subsequently fails.

Templates remain independent after creation. Re-enabling from a template creates a normal rule from the template's current draft values; it does not resurrect a hidden firewall object or bypass normal mutation validation.

Whether templates retain optional provenance such as "created from rule" or whether selected metadata is copied into a template is deferred to the detailed template/data-model design.

## Preparatory work before feature implementation

Before designing concrete metadata entities and endpoints, the existing UI should receive one focused structural preparation pass:

1. **Make family-relative position first-class in presentation.** Stop treating combined UFW numbering as the visible ordering model. Preserve the combined number only where authoritative snapshot/protocol semantics require it.
2. **Decompose the rule table at real interaction seams.** Extract family-level interaction from row presentation and establish independent match-context/details extension regions. Do not split cells/helpers merely to reduce file length.
3. **Generalize the projection vocabulary.** The existing rule-table projection increasingly represents a reusable rule-list presentation model rather than HTML-table state. Rename/generalize where that improves the boundary without speculative abstraction.
4. **Centralize page interaction mode.** Encode filtered vs. ordering-preview mutual exclusion once so later search/filter UI cannot accidentally leave reorder paths enabled.
5. **Preserve behavior and protocol boundaries.** No daemon protocol, signing, EF schema, or REST endpoint changes belong in this preparatory refactor.

After this preparation, search/filtering, ASP enrichment/grouping, and templates can be developed as separate feature slices over a stable presentation foundation.

## Deferred detailed design

The following are intentionally not fixed by this baseline and should be worked through in the feature-design phase:

- exact EF entities, keys, relationships, indexes, concurrency fields, and migrations;
- exact browser-facing REST contract and whether enriched contracts warrant a dedicated API-contract project;
- concrete metadata fields and grouping semantics;
- exact query grammar, filter controls, URL encoding, and highlighting rules;
- whether ordered insertion remains available while a filter is active;
- template schema, metadata-copy behavior, provenance, and template-manager UX;
- reconciliation endpoint/command shape and orphan-cleanup confirmation UX;
- whether IPv4/IPv6 remain stacked, become tabs, or move to separate routes;
- optional direct rule-detail navigation;
- any future automated orphan-retention policy.

These decisions must preserve the authority, identity, reconciliation, and interaction invariants above.
