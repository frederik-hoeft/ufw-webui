# Open tasks

This file is temporary, non-normative working storage for unresolved design and implementation work. Remove completed items after they reach the approved baseline; steady-state behavior belongs in the permanent architecture, protocol, deployment, development, or testing documentation.

## Reconcile IPv6 support into frontend capabilities

Expose the daemon's effective UFW IPv6 support/capability state through the existing operational/configuration reconciliation path, and use that authoritative capability to enable or disable IPv6 rule authoring controls in the frontend. The browser must not infer support from its own environment or assume that IPv6 is enabled merely because the structural rule model supports it. Preserve existing IPv4 behavior when IPv6 is unavailable, and keep the capability separate from per-rule address-family validation.

## Signed UFW presentation consistency check

Evaluate whether a future signed-intent protocol revision should include the canonical UFW rule text shown to the user and require the daemon to compare that signed presentation with the text rendered from the authoritative structural rule. Treat this only as a defense-in-depth consistency assertion; validated structural fields and direct argv execution remain the command-injection boundary. Any signed-intent payload/version change requires separate security design and approval.

## Frontend follow-ups after visual polish

### Evaluate address-family-separated rule tables

Consider replacing the inline `IPv4` / `IPv6` marker in the Direction column with distinct IPv4 and IPv6 rule tables rendered one after another. The goal is to make the primary rows visually cleaner while keeping address family explicit at the table level.

Before implementing this, resolve how the presentation maps to UFW's single authoritative ordered rule list:

- determine whether the two visual tables may reorder independently or whether cross-family ordering must remain visible/preservable;
- define how family-neutral (`Any family`) structural rules that may materialize into both IPv4 and IPv6 UFW rows are represented without implying two independently mutable rules;
- ensure row numbering, drag/drop, insert-before/after, and signed reorder occurrence IDs still refer unambiguously to authoritative UFW ordering rather than table-local positions;
- decide whether unsupported/read-only rows can cause inter-family ordering constraints that make a clean split misleading.

Treat this as a presentation/design task until the ordering semantics are settled. Do not silently change the signed mutation model to fit the visual grouping.
