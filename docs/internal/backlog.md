# Design Backlog

This document records unresolved larger design directions that are useful to retain between implementation phases. It is deliberately non-normative: none of these sections describes current product behavior unless a permanent architecture/protocol document says so.

## Rule templates and reversible disable workflow

Rule templates are ASP-owned authoring artifacts. They are reusable rule definitions, not live firewall rules, and they do not participate in authoritative UFW identity or ordering.

The core user stories are:

- save an existing live rule as a reusable template from the rule action menu;
- create, edit, and delete templates in a dedicated template manager without causing UFW side effects;
- start normal rule creation from a selected template so the existing editor is pre-populated with the template values;
- disable a live rule by preserving its reusable definition as a template and then deleting the live UFW rule;
- re-enable a rule by loading its template into the ordinary authoring/mutation flow rather than resurrecting hidden firewall state.

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
ordinary UFW mutation
```

Templates remain independent after creation. Updating a template does not alter any existing live rule, and creating a rule from a template does not create a persistent binding between the template and the resulting UFW rule unless a later feature gives such provenance a concrete use.

### Disable rule

**Disable rule** is an application workflow composed from template persistence plus the existing signed delete mutation; it should not introduce a special daemon-side firewall operation.

The safe failure order is:

1. persist the reusable template successfully;
2. issue the ordinary signed UFW delete;
3. if deletion fails, retain the template and report that the live rule is still active;
4. if deletion succeeds, clean up live-rule metadata according to the normal in-band deletion policy.

Persisting first is intentional. A failed delete can leave an extra template, whereas deleting first could lose the reusable rule definition if template persistence subsequently fails.

Detailed design is deferred until this feature becomes active work, including the template schema, uniqueness/naming rules, provenance, metadata-copy behavior, template ownership/auditing, and template-manager UX.

## First-class rule editing and replacement

Add a first-class **Edit rule** workflow that reuses the existing rule-authoring UI and signed mutation infrastructure while preserving the live rule's position and associated ASP metadata.

The browser-side workflow should treat editing as authoring a replacement rule rather than mutating fields in place. Selecting **Edit rule** loads the existing structural rule into the same reusable editor/components used by **Add rule**, while retaining the identity of the live rule being replaced. The editor can then validate and preview the replacement using the same capabilities as ordinary rule creation.

Conceptually:

```text
Authoritative live rule occurrence
    |
    | Edit
    v
FirewallRuleSpecification draft
    |
    | modify / validate / confirm / sign
    v
signed rule-replacement intent
    |
    | reconcile old occurrence, delete, insert replacement at same position
    v
new authoritative rule occurrence
```

### Identity and signed mutation contract

The update request needs to bind both sides of the replacement:

- the existing semantic rule identity/hash expected by the browser;
- the exact live occurrence being edited when duplicate semantic rules exist;
- the authoritative baseline snapshot/precondition used for reconciliation;
- the complete validated replacement rule model.

The old semantic SHA-256 identity is an important precondition, but it is not sufficient by itself to identify one live rule because multiple identical UFW occurrences can intentionally share the same semantic `RuleId`. The eventual signed intent should therefore reuse the existing occurrence/snapshot identity machinery rather than selecting an arbitrary matching hash when duplicates are present.

ASP should remain a forwarding/reconciliation boundary rather than becoming firewall authority. The daemon validates the signed replacement intent, reconciles the target against a fresh authoritative snapshot under the shared mutation gate, remembers its current family-local position, deletes exactly that occurrence, and inserts the replacement structural rule at the same position using the existing/adjacent insertion mechanics. On success it returns the resulting authoritative state and the replacement rule's new semantic identity.

The operation must not be documented or implemented as transactionally atomic unless the daemon can actually guarantee that property. A delete may succeed before replacement insertion fails, and a subsequent inventory refresh may itself become uncertain. The protocol therefore needs explicit success/partial-failure/state-uncertain outcomes and authoritative final-snapshot reconciliation, following the same conservative principles as reorder and batch-delete workflows. Compensation such as reinserting the old rule should be a deliberate protocol decision rather than an implicit best-effort side effect.

### Metadata reconciliation

Rule metadata remains keyed by semantic rule identity and must be reconciled only after the firewall result is known. In the ordinary case, notes, tags, and group membership associated with the edited rule should follow the replacement to its new semantic identity.

Duplicate semantic occurrences make this more subtle. If another live occurrence still uses the old semantic `RuleId`, the old metadata association must remain valid for that surviving rule while equivalent metadata is associated with the replacement's new identity. If the edited occurrence was the final live use of the old identity, the existing metadata row can instead be re-keyed/migrated or replaced as one logical operation. Reconciliation should derive this decision from the daemon's authoritative final snapshot rather than from the browser's pre-update assumptions.

This also implies that editing a rule whose replacement normalizes to the same semantic identity is a valid no-identity-change case: position is preserved and no unnecessary metadata churn should be required.

### UI integration

The rule action menu should expose **Edit rule** separately from **Edit metadata**. The edit workflow should reuse the existing Add Rule form and field components through an explicit create/edit mode instead of cloning the authoring UI. Editing state should carry the baseline occurrence identity and original semantic `RuleId` separately from the mutable draft.

The confirmation/signing step should make the replacement nature of the operation clear and show both the current canonical rule and the proposed canonical replacement. Successful completion should reconcile the ordinary rule inventory and metadata state from the returned authoritative result rather than relying on optimistic local substitution.

### Suggested implementation phases

1. **Reusable rule editor mode.** Extract or formalize the current Add Rule authoring state/components so the same validation, known-host suggestions, group/tag metadata inputs, and canonical preview can initialize from an existing live rule without duplicating form logic.
2. **Replacement intent/protocol.** Add a signed rule-replacement operation carrying the baseline snapshot/occurrence, old semantic identity, and new structural rule model. Define canonicalization, replay protection, result states, and exact failure semantics.
3. **Daemon reconciliation/execution.** Under the shared mutation gate, resolve the exact baseline occurrence, retain its position, delete it, insert the replacement at that position, then re-snapshot and return the reconciled result/new semantic identity.
4. **ASP metadata reconciliation.** Preserve/copy/re-key notes, tags, and group membership according to the authoritative post-update snapshot, including duplicate-old-identity and unchanged-identity cases.
5. **Client orchestration and UX.** Wire **Edit rule** into the rule actions menu, add replacement confirmation/signing, surface partial/uncertain outcomes, and reconcile the returned inventory without a separate speculative client model.
6. **Steady-state documentation and tests.** Reconcile the permanent protocol/security/architecture docs and cover duplicate semantic rules, cross-family behavior, normalization to the same hash, position preservation, insertion failure after successful deletion, stale baselines, replay, metadata migration, and uncertain final snapshots.

## Semantic firewall-policy exploration

Provide an exploratory view that answers questions about what the **UFW-managed policy represented by UFWeb** permits, using the same parsed/normalized rule semantics already exposed by the application.

The feature is intentionally a policy explorer rather than a full network simulator. It should help answer questions such as:

- given a source host or network, which destination address/service regions are permitted by the modeled UFW rule set?
- given a destination host/network and optional service, which source address regions are permitted to access it?
- given both source and destination constraints, which protocols/ports remain permitted or denied?
- which rule/default-policy decision is responsible for a particular included or excluded region?

The UI should describe these results as **permitted/denied by the modeled UFW policy**, not as proof of real network reachability. Actual reachability also depends on routing, topology, host/service availability, NAT, other firewalls, kernel state, and policy outside the modeled UFW surface.

### Semantic model

At a high level, exploration operates over normalized packet-space regions rather than individual packets. A useful conceptual domain is the Cartesian product of the dimensions that UFWeb can model reliably, for example:

```text
source address space
  × destination address space
  × protocol
  × source/destination port space where applicable
  × UFW direction / forwarding context
  × interface constraints where represented
```

CIDR networks become address sets and port/range expressions become integer sets. Rule application can then be expressed as ordered set operations over those regions: accepted/denied portions are accumulated with union operations, while portions already decided by an earlier terminal rule are removed from the still-undecided space with set difference. Default policy resolves whatever modeled region remains after the ordered rule set has been applied.

Conceptually, for an undecided packet region \(U\), the region \(R_i\) matched by rule \(i\), and one decision set \(D_a\) per modeled rule action:

```text
matched_i       = U ∩ R_i
U               = U \ matched_i
D[action_i]     = D[action_i] ∪ matched_i
```

The applicable default policy classifies whatever remains in `U` after the ordered rule set has been evaluated. `Allow`, `Deny`, and `Reject` can therefore be represented as ordinary terminal decision regions. `Limit` is rate-dependent rather than a static allow/deny result and should remain a distinct conditional/rate-limited decision unless a later model explicitly includes the runtime state needed to resolve it.

The exact internal representation should be chosen for correctness and tractable simplification rather than for mirroring UFW syntax. IPv4/IPv6 address ranges, port intervals, protocol domains, and other finite dimensions should have focused set-algebra primitives that can split, intersect, subtract, normalize, and coalesce regions without expanding CIDRs or port ranges into individual values.

### Scope and semantic boundary

The initial explorer should evaluate only state that UFWeb can authoritatively obtain and normalize from the managed UFW configuration. In particular, the first version should explicitly disregard or treat as outside the model:

- conntrack/runtime connection state;
- arbitrary `before.rules`, `after.rules`, or externally managed netfilter/nftables rules that are not represented by the parsed UFW rule model;
- NAT and address/port translation unless it later becomes an explicitly modeled input;
- routing-table decisions and multi-hop topology;
- downstream/remote firewall policy;
- whether a destination host or service actually exists or is listening.

The explorer should surface this scope prominently enough that a user cannot reasonably confuse policy analysis with an end-to-end connectivity test.

### Exploration modes

The eventual interaction model can support increasingly constrained questions over the same evaluator rather than separate implementations:

```text
Source-centric
  source = host/network
  destination = unconstrained or optionally constrained
  -> show permitted/denied destination and service regions

Destination-centric
  destination = host/network[/service]
  source = unconstrained or optionally constrained
  -> show permitted/denied source regions

Pairwise
  source + destination supplied
  -> show the effective protocol/port policy and explain the deciding rules
```

A useful result should preserve provenance. When a region is allowed, denied, or split, the presentation should be able to explain which ordered UFW rule or default policy produced that decision. The evaluator therefore should return structured decision/evidence data rather than only a flattened list of resulting CIDRs and ports.

### Suggested development phases

A future implementation can be staged so that the semantic engine is independently useful and testable before a complex UI is built:

1. **Normalized set algebra.** Introduce well-tested address/network and port-range set primitives with intersection, subtraction, union/coalescing, containment, and canonicalization for both IP families.
2. **Ordered UFW policy evaluator.** Project the existing parsed firewall model into semantic regions, apply ordered terminal rule/default-policy semantics, and return structured decision regions with provenance. Keep this layer independent from Razor and database state.
3. **Source/destination exploration UI.** Add family-aware inputs and render permitted/denied regions plus the rules responsible for them. Start with bounded, textual/tabular results rather than speculative topology visualization.
4. **Explanation and navigation.** Link decision evidence back to the corresponding live rule presentation, allow users to refine a result into a more constrained query, and add useful aggregation/simplification for large result sets.
5. **Optional extensions.** Only after concrete use cases justify them, consider additional modeled dimensions, saved exploration scenarios, exports, or richer visualization.

Correctness testing should emphasize overlapping CIDRs, partially overlapping port ranges, rule-order shadowing, default-policy fallthrough, mixed allow/deny partitions, interface constraints, forwarding versus host-local rules, and independent IPv4/IPv6 behavior. Property-based tests are likely valuable for the set-algebra core because apparently simple subtraction/coalescing bugs can silently produce incorrect security conclusions.

## Rule-query navigation and power-user syntax

The current browser query model is already a composable collection of structured filters with typed match evidence. Two optional presentation features can build on that model without introducing a second search implementation:

- **Navigable query state.** Encode the configured filter collection in the `/rules` route so a reload or shared URL can reconstruct the same client-side query after fetching a fresh authoritative snapshot. The encoding should be designed for a variable collection of typed filters rather than assuming one scalar query parameter per filter field.
- **Shorthand query grammar.** If real usage justifies it, accept compact expressions such as `from:10.0.0.0/8 proto:tcp tag:observability "prometheus"` and compile them into the same existing filter models. Unqualified text should remain ordinary text search rather than being inferred aggressively as addresses/ports/tags.

Sorting remains conceptually separate from filtering. Any future non-firewall sort mode must keep reordering unavailable because only complete firewall order has mutation meaning. These features should remain browser projections; a server-side `/rules/search` endpoint should be introduced only for a concrete server-authoritative use case.

## Signed presentation consistency hardening

Evaluate whether a future signed-intent protocol revision should include the canonical UFW rule text shown to the administrator and require the daemon to compare that signed presentation with text rendered independently from the authoritative structural rule. This would be a defense-in-depth consistency assertion, not the command-injection boundary: validated structural fields and direct argv execution remain authoritative.

Because this changes the signed payload, it requires an explicit protocol-version/security design rather than being added as an incidental UI check.

## Backlog discipline

These items are not implementation commitments or a schedule. Each should receive a focused design/implementation plan when it becomes active work. Future work must preserve the existing authority split: UFW remains authoritative for live firewall rules, ASP may own authoring/presentation context, and browser-only presentation state must not become firewall authority. Completed behavior belongs in permanent documentation rather than accumulating here.
