# Rule-presentation foundation refactor plan

This plan defines the preparatory frontend refactor that should land before the rule-presentation and enrichment sprint. It deliberately stops at presentation structure and ordering semantics: search/filtering, ASP-backed metadata, grouping, templates, EF entities, and new REST contracts belong to the feature sprint and are not implemented here.

The goal is to remove the remaining coupling between authoritative UFW coordinates, family-local presentation, and the current monolithic `RuleTable` before richer rule state starts flowing through the UI. The resulting frontend should treat IPv4 and IPv6 as independent presentation workspaces over one authoritative firewall snapshot, while keeping the shared page lifecycle and mutation orchestration in one place.

## Goals

The refactor should establish the following steady-state foundation:

- UFW's combined numbered-list position remains authoritative transport/protocol data and is never repurposed as a client-side presentation coordinate.
- visible rule positions are family-relative and are derived from the current presentation order;
- ordering previews carry occurrence order, not cloned `ListedFirewallRule` instances with synthetic UFW numbers;
- the reusable projection layer describes a rule list and its family projections rather than an HTML table;
- IPv4 and IPv6 are selected through tabs and rendered as one family workspace at a time;
- family-local interaction state, especially drag/drop and move-to-position state, is owned below the page level;
- desktop rows and mobile cards are reusable presenters over the same row projection;
- family-specific behavior is isolated behind focused policies/services only where IPv4 and IPv6 actually differ;
- the route/page continues to own the authoritative snapshot, refresh lifecycle, signed mutations, ordering-preview application/discard, and global firewall state.

This is a structural refactor. Existing firewall behavior, signing, daemon IPC, ordered insertion, deletion, and refresh/reconciliation semantics must remain unchanged.

## Explicit non-goals

Do not pull feature work from the next sprint into this branch. In particular, this refactor does **not** add:

- free-text search, structured filters, filter chips, highlighting, match-context expansion, or query URLs;
- ASP-backed rule metadata or metadata expansion;
- groups or group management;
- rule templates, disable-as-template behavior, or template management;
- EF entities or migrations;
- enriched browser-facing REST DTOs/endpoints;
- metadata orphan reconciliation;
- arbitrary alternate sorting of firewall rules;
- separate routed IPv4 and IPv6 pages.

The tab layout should leave clean seams for those features without adding placeholder abstractions that have no current behavior.

## Current coupling to remove

The existing client already understands family-local ordering in several places, but the presentation still mixes three coordinate systems and responsibilities:

1. `ListedFirewallRule.DisplayNumber` is the authoritative combined UFW number returned by the daemon.
2. `RuleRowProjection.FamilyPosition` is the family-relative coordinate used by move operations.
3. `RuleOrderingProjectionService` currently creates cloned `ListedFirewallRule` instances and rewrites `DisplayNumber` to express a local ordering preview.

`RuleTableProjectionService.CreatePositionChange()` then compares an occurrence's original combined-list index to the rewritten `DisplayNumber`. This works visually for the current table but makes presentation state masquerade as authoritative UFW state.

At the component level, `RuleTable` also owns too many unrelated concerns at once:

- family grouping and headings;
- desktop table rendering;
- mobile card rendering;
- drag source/drop target state;
- directional drop-indicator calculation;
- move-to-position dialog orchestration;
- row action dispatch;
- ordering-preview presentation;
- read-only/opaque rule presentation.

Cross-family drag is prevented by runtime checks even though it should be impossible by construction once each family has its own workspace.

## Target data-model separation

### Authoritative rule data

`ListedFirewallRule` remains the snapshot model received from the backend. Its fields preserve daemon/UFW semantics.

In particular:

```text
ListedFirewallRule.DisplayNumber
    = authoritative UFW combined-list coordinate
    = snapshot/protocol concern
    = never rewritten by presentation code
```

The client may display the authoritative number in diagnostics where useful, but it is not the primary rule ordinal in the rules workspace.

### Occurrence identity

Occurrence identity remains snapshot-local. An occurrence ID identifies one row in the authoritative snapshot even when multiple rows share the same semantic rule hash.

The preparatory refactor should keep occurrence identity independent from both UFW display numbers and family-relative positions:

```text
semantic RuleId
    stable semantic rule identity

occurrence ID
    snapshot-local identity for one listed occurrence

DisplayNumber
    authoritative combined UFW coordinate

FamilyPosition
    current family-local presentation coordinate
```

None of these values should be silently substituted for another.

### Ordering preview

`RuleOrderingPreview` should describe the desired occurrence order and which occurrences were directly moved. It should no longer own a second list of cloned `ListedFirewallRule` objects solely to carry rewritten numbers.

Conceptually:

```text
Authoritative snapshot
    rules[0..n]
        |
        + RuleOrderingPreview.DesiredOrder
        v
Projected occurrence order
        |
        v
RuleListProjectionService
        |
        + family grouping
        + family-relative positions
        + position-change annotations
        v
RuleListProjection
```

The page should therefore stop selecting between `snapshot.Rules` and `orderingPreview.Rules`. It should always project the authoritative snapshot together with the optional preview.

### Family-relative position changes

Ordering-preview annotations should compare family-local positions, not UFW combined positions.

For example, with interleaved authoritative rows:

```text
UFW #1  IPv4 A
UFW #2  IPv6 X
UFW #3  IPv4 B
UFW #4  IPv6 Y
```

the visible positions are:

```text
IPv4: A=1, B=2
IPv6: X=1, Y=2
```

Moving IPv4 B before A produces `B: 2 -> 1` and `A: 1 -> 2`. IPv6 X/Y retain positions 1/2 regardless of the combined UFW coordinates used by the authoritative snapshot.

`RulePositionChange` should therefore mean "family position changed" throughout the client.

## Generalized presentation projection

The current `RuleTableProjection` vocabulary should be generalized because the model drives desktop tables, mobile cards, tabs, ordering, and future enriched presentation. It is not an HTML-table model.

A suitable direction is:

```text
IRuleListProjectionService
    Create(authoritativeRules, orderingPreview)
        -> RuleListProjection

RuleListProjection
    Families
        IPv4 -> RuleFamilyProjection
        IPv6 -> RuleFamilyProjection

RuleFamilyProjection
    AddressFamily
    Rows
    Count

RuleRowProjection
    Rule                    authoritative ListedFirewallRule
    AddressFamily
    OccurrenceId
    FamilyPosition
    FamilyCount
    CanOrder
    CanMutate
    PositionChange?
```

Exact type names may change during implementation if a clearer model emerges, but the boundary should remain presentation-oriented and rendering-agnostic.

The projection service is responsible for deterministic derived state:

- observing each rule's address family;
- counting semantic duplicates for mutation capability;
- assigning snapshot occurrence IDs;
- applying an ordering preview to occurrence order;
- grouping projected occurrences by family;
- assigning family-relative positions and counts;
- deriving direct/indirect family-position changes;
- preserving opaque/read-only rows rather than dropping them.

It must not own Razor state, dialogs, DOM behavior, future query state, or ASP metadata persistence.

## Page and family boundaries

### `Rules` page

The routed page remains the owner of state that belongs to the firewall as a whole:

- loading and refreshing the authoritative snapshot;
- stale/error state;
- firewall/default-policy/capability presentation;
- delete and ordered-insertion orchestration;
- ordering-preview creation, authorization, apply/discard, and result handling;
- global busy/mutation capability;
- selected family tab.

The page should not own row expansion state, drag targets, or desktop/mobile row rendering.

### Tab-based family selection

The rules area should present IPv4 and IPv6 as tabs within the same `/rules` page. Only the selected family's workspace is rendered as the primary rule list.

The family tabs are a navigation/presentation boundary, not two independent firewall pages:

```text
/rules
└─ Rules page
   ├─ global firewall state/default policies/actions
   └─ family area
      ├─ IPv4 tab (count)
      ├─ IPv6 tab (count)
      └─ RuleFamilyWorkspace for selected family
```

Tab counts represent the complete family projection. They are not query-result counts; filtering does not exist in this preparatory branch.

IPv4 should be the normal initial selection. If a previously selected family is no longer available after refresh, selection falls back deterministically to an available family. The UI must not hide authoritative IPv6 rows merely because current capability state changes; tab availability should take both current capability and observed snapshot content into account where necessary.

No separate `/rules/ipv4` or `/rules/ipv6` routes are introduced. The component boundary should nevertheless make a future navigation/layout change a composition decision rather than a rewrite.

### `RuleFamilyWorkspace`

A family workspace is the primary interaction boundary for one `RuleFamilyProjection`. It should own family-local ephemeral state and interaction orchestration:

- drag source and current drop target;
- drag/drop handlers and drop-indicator state;
- move-to-position dialog invocation;
- dispatching family-local move requests;
- selecting the desktop or mobile presentation through responsive markup/styles already used by the application;
- rendering family-local empty/read-only states where appropriate.

Because the workspace receives only one family projection, cross-family drag/drop should become impossible without explicit family comparisons in drag handlers.

The workspace should receive mutation/ordering capability from the page as parameters. It should not infer page-global stale/busy state or perform API calls itself.

## Row/card decomposition

Desktop and mobile rule representations are legitimately different DOM structures. Do not force them through Razor inheritance or an over-generic render-fragment framework.

Use composition instead:

```text
RuleFamilyWorkspace
├─ RuleDesktopList/Table
│  └─ RuleDesktopRow*
└─ RuleMobileList
   └─ RuleMobileCard*
```

Whether the desktop table shell remains inside `RuleFamilyWorkspace` or becomes a tiny `RuleDesktopList` component should be decided by cohesion during implementation. The important boundary is that one row/card presenter receives one `RuleRowProjection` and explicit callbacks rather than reaching back into page state.

Row/card presenters should own presentation such as:

- family-relative position and position-change indicator;
- action/direction/endpoints/protocol/comment formatting;
- drag handle rendering supplied with workspace-owned drag callbacks/state;
- opaque/read-only rendering;
- row actions and explicit event callbacks.

They should not own:

- ordering-preview algorithms;
- API calls;
- dialogs whose outcome changes family ordering;
- page refresh/stale handling;
- future metadata lookup/search evaluation.

Do not extract individual cells into components simply to shorten files. Component boundaries should correspond to reusable presentation or interaction responsibilities.

## IPv4/IPv6 specialization

The tab/workspace components themselves should be family-agnostic. Avoid parallel `IPv4RuleFamilyWorkspace` and `IPv6RuleFamilyWorkspace` components, and avoid Razor component inheritance.

Where behavior genuinely differs by address family, prefer a focused domain policy/service that can be selected for the active family. Examples in later feature work may include IPv4 versus IPv6 address/CIDR parsing or matching. Shared behavior such as actions, directions, protocols, tags, ordering, and row layout should remain common.

This preparatory branch should **not** introduce empty IPv4/IPv6 strategy interfaces merely in anticipation of search. Only extract a family-specific abstraction if the current refactor exposes a real differing responsibility. The main objective now is to ensure future family-specific logic has a clean workspace/projection boundary in which to live.

Existing shared firewall normalizers/validators remain authoritative for domain validation; do not duplicate address-family rules in presentation components.

## Ordering behavior to preserve

The refactor must preserve the current ordering contract:

- reordering is family-local;
- opaque/unparsed rows remain visible and cannot be directly reordered when the existing capability rules disallow it;
- semantic duplicates remain read-only for mutations that require unique semantic identity;
- drag/drop and move-to-position produce the same `RuleMoveRequest` family-relative target semantics;
- direct versus indirect position changes remain visually distinguishable;
- applying a preview still signs and submits a desired order over authoritative snapshot occurrences;
- refresh/discard clears the local preview according to the current page lifecycle;
- ordered insertion anchors continue to refer to authoritative rule occurrences and existing navigation semantics.

The directional drop indicator fix must remain intact: when dragging downward over a target row, the indicator appears after that target; when dragging upward, it appears before the target.

## Frontend layout scope

The preparatory visual change is intentionally limited to family selection and structural composition. The target rules-page hierarchy is:

```text
page heading + global actions
firewall state / default policies
family tabs (IPv4 / IPv6 with counts)
selected family workspace
    rule list/table/cards
```

The implementation should follow the existing visual language rather than reproduce a generated mockup pixel-for-pixel.

Specifically out of scope for this branch are the future search input, filter controls/chips, sort controls, match-result expansion, metadata details, and template actions. The family workspace should have enough structural room for those controls to be added later without another family-boundary refactor.

The natural list order remains firewall order. Do not add generic sorting in preparation for the next sprint; alternate sorting would have interaction implications similar to filtering and requires its own product decision.

## Implementation sequence

### 1. Separate authoritative and projected ordering data

- Remove presentation rewriting of `ListedFirewallRule.DisplayNumber` from `RuleOrderingProjectionService`.
- Reduce `RuleOrderingPreview` to authoritative occurrence-order state plus direct-move tracking.
- Make the presentation projection apply the optional desired occurrence order to the authoritative snapshot.
- Remove `Rules.DisplayedRules` or any equivalent path that swaps authoritative rules for cloned preview rules.
- Keep `DisplayNumber` unchanged from the daemon response throughout the client.

Validate this step with focused ordering/projection tests before moving component state.

### 2. Make family-relative numbering complete

- Derive current family positions after applying the preview order.
- Derive original family positions from the authoritative occurrence order.
- Express `RulePositionChange` entirely in family-local coordinates.
- Render family position as the primary ordinal in desktop and mobile rule presentation.
- Update localization/accessibility text that currently describes visible numbers as UFW numbers where necessary.
- Retain authoritative UFW numbering only where it is semantically required outside primary presentation.

### 3. Generalize the projection boundary

- Rename `IRuleTableProjectionService` / `RuleTableProjectionService` / `RuleTableProjection` to rule-list terminology where appropriate.
- Keep `RuleFamilyProjection` and `RuleRowProjection` rendering-agnostic.
- Update DI registration and tests.
- Avoid adding next-sprint fields to these records prematurely.

### 4. Introduce tab-based family composition

- Move family selection to the rules page/family-area boundary.
- Render family tabs with stable complete-family counts.
- Pass only the selected `RuleFamilyProjection` into the active workspace.
- Define deterministic selection/fallback across refreshes and IPv6 capability changes.
- Preserve accessibility semantics and keyboard behavior through the existing component library where possible.

### 5. Extract the family interaction workspace

- Move drag source/drop target state and move-dialog orchestration out of `RuleTable` into `RuleFamilyWorkspace`.
- Eliminate cross-family checks that become structurally impossible.
- Preserve non-rendering high-frequency drag-event handling; do not reintroduce a Blazor render for every `dragover` event.
- Keep move/delete/insertion requests as explicit callbacks to the page.

### 6. Extract row/card presenters

- Split parsed/read-only desktop and mobile presentation into focused row/card components where this removes meaningful duplication/coupling.
- Share lower-level formatting helpers/services rather than using Razor inheritance.
- Keep action dispatch explicit and testable.
- Do not create placeholder search-match or rich-metadata components in this branch.

### 7. Consolidate styles and remove obsolete structure

- Reorganize rule-list Sass around the new page/family/row ownership boundaries.
- Keep component-private selectors nested under their owning component classes.
- Remove obsolete family-section/table selectors only after equivalent responsive behavior is verified.
- Preserve light/dark theme behavior and current mobile presentation.

## Validation and regression tests

At minimum, the refactor should add or update tests for these invariants:

- authoritative `ListedFirewallRule.DisplayNumber` values are unchanged by preview creation;
- interleaved IPv4/IPv6 authoritative rows receive independent family positions;
- moving a rule changes positions only inside its family;
- direct and indirectly shifted rows receive correct original/current family positions;
- desired occurrence order sent to reorder remains equivalent to current behavior;
- duplicate semantic rules retain existing mutation restrictions;
- opaque/read-only rows remain represented in the correct family/order;
- drop-indicator direction remains correct for upward and downward moves;
- tab counts equal complete family counts;
- changing tabs does not alter or recreate authoritative snapshot state;
- selected-family fallback is deterministic when the available family set changes;
- IPv6 rules are not hidden merely because IPv6 capability becomes disabled;
- desktop/mobile presenters emit the same rule semantics and action callbacks they do before the refactor.

Run the full solution build/test suite, formatter verification, and `git diff --check` before review. Because the branch is intended to be behavior-preserving, any changed daemon IPC, signing payload, REST behavior, or EF schema is a regression unless explicitly required to preserve existing behavior.

## Completion criteria

The preparatory refactor is ready when:

- the authoritative UFW snapshot remains untouched by presentation/order-preview code;
- visible numbering and ordering-preview annotations are entirely family-relative;
- rule-list projection terminology no longer assumes one HTML table;
- the `/rules` page uses IPv4/IPv6 tabs and renders one independently composed family workspace;
- family-local drag/drop/move state no longer lives in a monolithic cross-family `RuleTable`;
- desktop/mobile row presentation is isolated enough to accept future metadata and match sections without returning family interaction state to the row component;
- no search/filtering, metadata, templates, EF, or new REST feature surface has leaked into the refactor;
- existing ordering, insertion, deletion, refresh, signing, responsive layout, and accessibility behavior remains intact;
- the full branch diff is reviewable as one coherent presentation-foundation change.

Once merged, the next sprint can design the enriched browser contract, metadata entities, search/query model, templates, and reconciliation behavior against this stable family-local presentation foundation.
