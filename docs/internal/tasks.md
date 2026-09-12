# Open tasks

This file is temporary, non-normative working storage for unresolved design and implementation work. Remove completed items after they reach the approved baseline; steady-state behavior belongs in the permanent architecture, protocol, deployment, development, or testing documentation.

## Firewall rule ordering backend and signed mutation contract

The browser-side ordering UX remains implemented against `IRuleOrderingApiClient`, with `MockRuleOrderingApiClient` as the registered implementation until the backend mutation flow is approved and implemented. The proposed steady-state architecture is documented in [Firewall Rule Reordering Design](rule-reordering-design.md).

The draft design settles the main architectural choices: the browser signs an exact authoritative baseline fingerprint plus the complete desired permutation; rows use snapshot-local occurrence IDs rather than semantic `ruleId` values; the daemon verifies that baseline under the serialized UFW execution gate; move planning uses a minimal longest-increasing-subsequence formulation; and each delete/reinsert move carries a mandatory recovery obligation with structured partial-execution reporting.

Implementation remains gated on maintainer approval of that design and on validating the UFW representation needed for lossless reinsertion. Ordered rule creation remains a separate future mutation contract rather than an extension of the reorder request.

## Reliable mutation reconciliation from UFW output

The daemon currently reconciles successful add/delete operations by reparsing `ufw status numbered` and matching the resulting structural rule identity. The development mock exposes a lossy round-trip for at least one valid rule shape: `route reject from 0.0.0.0/0 to 10.100.200.2 proto udp` is rendered by `Ufw.Mock status numbered` as `10.100.200.2 REJECT FWD Anywhere`, with no protocol marker because neither endpoint has a port. The parser therefore reconstructs the observed rule with protocol `Any`, so the post-add semantic identity does not match the signed UDP specification even though the mock successfully created the rule. The current safe response is an uncertain-mutation error and an authoritative refresh requirement rather than falsely reporting success.

First verify the corresponding output against real UFW. If real `ufw status numbered` retains enough information, this is a mock-fidelity bug and the mock should be corrected to match UFW. If the real status representation is also lossy, design a reconciliation source/model that preserves all semantics required by `RuleIdentity` without weakening identity comparison globally. In particular, do not simply ignore protocol during matching: TCP/UDP distinctions are security-relevant and may be observable in other rule shapes. Candidate approaches include using a richer UFW representation such as the added/user-rules form, correlating the pre/post snapshot delta with a canonical command representation under strict ambiguity checks, or introducing daemon-owned metadata only if that can coexist safely with externally managed UFW rules. The solution must continue to handle externally-created rules, duplicate semantic rules, address-family expansion, cancellation, and ambiguous post-mutation state conservatively.

Add regression coverage for protocol-only rules without ports and any other rule fields that the selected authoritative representation cannot round-trip before changing the mutation success criteria. This requires explicit daemon/backend design approval unless verification shows the issue is isolated to `Ufw.Mock` fidelity.

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
- ensure row numbering, drag/drop, insert-before/after, and future signed ordering intents still refer unambiguously to authoritative UFW ordering rather than table-local positions;
- decide whether unsupported/read-only rows can cause inter-family ordering constraints that make a clean split misleading.

Treat this as a presentation/design task until the ordering semantics are settled. Do not silently change the signed mutation model to fit the visual grouping.
