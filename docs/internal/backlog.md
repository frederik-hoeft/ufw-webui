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

## Semantic firewall-policy exploration UI

The read-only policy model and ordered evaluator are specified in [Semantic policy domain](../architecture/domain.md). What remains is the browser workflow that asks that model questions and presents the partition.

The UI should describe results as **permitted or denied by the modeled UFW policy**, not as proof of real network reachability. The same scope limits already stated for the domain model need to stay visible in the interaction: connection tracking, rules outside the parsed user chain, NAT, routing, and host or service availability are not what the screen is answering.

Useful first questions, all of which are constraints on one evaluation rather than separate engines:

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

Presentation should keep the partition's provenance, so a region can be tied back to the ordered rule or default policy that produced it. Later navigation can link that evidence to the live rule, let the user tighten the same result, and add aggregation when a partition is too large to read as a raw rectangle list. Saved scenarios, exports, and richer visualization wait on a concrete use. Topology diagrams are not part of this work.

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
