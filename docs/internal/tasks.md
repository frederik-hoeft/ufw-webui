# Open tasks

This file is temporary, non-normative working storage for unresolved design and implementation work. Remove completed items after they reach the approved baseline; steady-state behavior belongs in the permanent architecture, protocol, deployment, development, or testing documentation.

## Reconcile IPv6 support into frontend capabilities

Expose the daemon's effective UFW IPv6 support/capability state through the existing operational/configuration reconciliation path, and use that authoritative capability to enable or disable IPv6 rule authoring controls in the frontend. The browser must not infer support from its own environment or assume that IPv6 is enabled merely because the structural rule model supports it. Preserve existing IPv4 behavior when IPv6 is unavailable, and keep the capability separate from per-rule address-family validation.

## Ordered rule creation

Implement signed insert-before/insert-after rule creation as a state-conditioned mutation contract. Reordering existing rows uses an exact baseline fingerprint plus a complete occurrence permutation; creation changes membership and placement together, so it has its own signed placement semantics and stale-state behavior rather than extending `rules.reorder`. See [Ordered Rule Insertion Implementation Plan](ordered-rule-insertion-implementation-plan.md).

## Signed UFW presentation consistency check

Evaluate whether a future signed-intent protocol revision should include the canonical UFW rule text shown to the user and require the daemon to compare that signed presentation with the text rendered from the authoritative structural rule. Treat this only as a defense-in-depth consistency assertion; validated structural fields and direct argv execution remain the command-injection boundary. Any signed-intent payload/version change requires separate security design and approval.

## Known hosts authoring aliases

Add an ASP-owned **known hosts** management feature, conceptually similar to the network-interface metadata workflow, so administrators can maintain reusable host aliases for rule authoring instead of repeatedly entering literal IP addresses. This is an authoring convenience only: UFW and the daemon remain authoritative for firewall semantics, and selecting a known host must resolve to the underlying literal IP address before canonical rule rendering, signing, validation, IPC, or daemon dispatch. Do not extend the signed intent contract with ASP-only host IDs, names, or comments.

The initial design should cover:

- Persist known hosts in the ASP database using the project's normal WKG/EF mapping conventions, with an internal numeric primary key and a UUIDv7 frontend-facing identifier. Store the literal IP address plus human-facing metadata suitable for identification/search (for example a short name and/or comment; settle the exact presentation model before freezing the schema).
- Provide ASP API and frontend management flows to list, create, edit, and delete known-host entries. Deleting or changing an alias must not mutate existing firewall rules; aliases are lookup metadata for future authoring only.
- Integrate known hosts into source/destination address authoring. Autocomplete/search should support useful substring matching over the literal address and human-facing metadata while preserving free-text address entry for hosts or networks that are not in the ASP catalog.
- When a known host is selected, write only its literal IP address into `FirewallRuleSpecification`. The canonical UFW preview and browser-side signed intent must therefore be indistinguishable from manually entering that same IP address.
- Validate address-family compatibility and malformed/stale entries at the authoring/API boundaries. A stored IPv4 alias must not silently become an IPv6 value (or vice versa), and changing an alias after a rule was authored must not retroactively change the rule being signed or displayed.
- Keep the feature independent of daemon inventory/reconciliation unless a separate host-discovery requirement is introduced later. Unlike network interfaces, these records are intentionally ASP-owned rather than a cache of daemon-owned state.

Consider whether a visibility/show-in-suggestions flag is useful for parity with network-interface authoring, but do not make hidden entries affect firewall validity: visibility would be a presentation preference only.

## Frontend follow-ups after visual polish

### Evaluate address-family-separated rule tables

Consider replacing the inline `IPv4` / `IPv6` marker in the Direction column with distinct IPv4 and IPv6 rule tables rendered one after another. The goal is to make the primary rows visually cleaner while keeping address family explicit at the table level.

Before implementing this, resolve how the presentation maps to UFW's single authoritative ordered rule list:

- determine whether the two visual tables may reorder independently or whether cross-family ordering must remain visible/preservable;
- define how family-neutral (`Any family`) structural rules that may materialize into both IPv4 and IPv6 UFW rows are represented without implying two independently mutable rules;
- ensure row numbering, drag/drop, insert-before/after, and signed reorder occurrence IDs still refer unambiguously to authoritative UFW ordering rather than table-local positions;
- decide whether unsupported/read-only rows can cause inter-family ordering constraints that make a clean split misleading.

Treat this as a presentation/design task until the ordering semantics are settled. Do not silently change the signed mutation model to fit the visual grouping.
