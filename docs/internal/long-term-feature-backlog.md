# Long-term feature backlog

This document is a strategic backlog for larger user-facing capabilities that should be developed after the current [rule-presentation and enrichment sprint](rule-presentation-sprint-plan.md). It records durable product direction and the main architectural constraints that future planning should preserve, but it is not a detailed implementation plan, API specification, or schedule.

Each feature should receive its own focused design/implementation plan when it becomes active work. Completed behavior should move into the permanent architecture, protocol, deployment, or user documentation rather than accumulating here indefinitely.

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

## Semantic firewall-policy exploration

Provide an exploratory view that answers questions about what the **UFW-managed policy represented by UFW WebUI** permits, using the same parsed/normalized rule semantics already exposed by the application.

The feature is intentionally a policy explorer rather than a full network simulator. It should help answer questions such as:

- given a source host or network, which destination address/service regions are permitted by the modeled UFW rule set?
- given a destination host/network and optional service, which source address regions are permitted to access it?
- given both source and destination constraints, which protocols/ports remain permitted or denied?
- which rule/default-policy decision is responsible for a particular included or excluded region?

The UI should describe these results as **permitted/denied by the modeled UFW policy**, not as proof of real network reachability. Actual reachability also depends on routing, topology, host/service availability, NAT, other firewalls, kernel state, and policy outside the modeled UFW surface.

### Semantic model

At a high level, exploration operates over normalized packet-space regions rather than individual packets. A useful conceptual domain is the Cartesian product of the dimensions that UFW WebUI can model reliably, for example:

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

The initial explorer should evaluate only state that UFW WebUI can authoritatively obtain and normalize from the managed UFW configuration. In particular, the first version should explicitly disregard or treat as outside the model:

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

## Backlog discipline

Features in this document are deliberately outside the current rule-presentation/enrichment sprint unless explicitly promoted into an active plan. Future work should preserve the existing authority split: UFW remains authoritative for live firewall rules, ASP may enrich or provide application-owned authoring/context state, and the browser owns presentation interaction that does not need server authority.
