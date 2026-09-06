# Open tasks

This file tracks only open project work. Remove an item when the corresponding work is completed and merged into the approved baseline.

## Firewall rule ordering backend and signed mutation contract

The browser-side ordering UX is implemented against `IRuleOrderingApiClient`, with `MockRuleOrderingApiClient` as the only registered implementation. Existing parsed rules can be reordered by drag handle or an explicit one-based "Move to position..." dialog. Mock moves update only a local browser projection, are clearly labeled as non-authoritative, disable real add/delete mutations while active, and are discarded by authoritative refresh. No private key is collected because no signed ordering operation exists yet.

The rule menu also carries insertion intent into the create flow through `/rules/create?before=<rule-id>` and `/rules/create?after=<rule-id>`. The create page resolves and displays that target but deliberately disables submission. The existing signed `rules.add` operation remains append-only. No real HTTP implementation or endpoint URI is encoded for `IRuleOrderingApiClient`; doing so would prematurely freeze a contract before the authorization and daemon semantics are designed.

Remaining design work spans REST, signed intent, daemon execution, and UFW reconciliation:

- Define a safe snapshot/order precondition. `ruleId` is a semantic identity rather than a unique row identity: externally-created duplicate rules can share one ID, while unsupported/read-only UFW rows have no mutable ID at all. Ordering needs an unambiguous target/addressing model and must conflict rather than guess when the authoritative order changed.
- Choose the operation model: absolute one-based move, move-before/move-after an anchor, or another representation. The signed canonical payload must make the requested final placement unambiguous.
- Define ordered creation separately from append-only `rules.add`, including whether insertion is a new intent operation or a versioned extension of add. The current browser query parameters are presentation state, not a protocol proposal.
- Define user authorization and canonical signing fields for reorder/insert requests, including any authoritative snapshot token/revision used as a stale-order precondition. This requires explicit security-protocol approval and may require an intent protocol version change.
- Define daemon execution and failure semantics. UFW supports positional insertion, but moving an existing rule may require a compound delete/insert sequence. The daemon must retain its serialized execution gate across the whole mutation, define rollback/recovery behavior for partial subprocess failure, and reconcile authoritative state before success.
- Resolve address-family expansion and numbering behavior for family-neutral ordered creation, where one structural add can materialize as multiple concrete UFW rows.
- Decide whether unsupported/read-only rows participate as movable anchors, immutable ordering barriers, or only snapshot positions. The browser currently disables direct ordering of rows it cannot address safely.
- Define the successful mutation response and post-mutation browser reconciliation path. The final implementation should replace the mock DI registration rather than preserve the local projection as authoritative state.

## Signed UFW presentation consistency check

Evaluate whether a future signed-intent protocol revision should include the canonical UFW rule text shown to the user and require the daemon to compare that signed presentation with the text rendered from the authoritative structural rule. Treat this only as a defense-in-depth consistency assertion; validated structural fields and direct argv execution remain the command-injection boundary. Any signed-intent payload/version change requires separate security design and approval.

## Replace static helpers with DI services

Review reusable static helper classes, especially in shared libraries, and convert stateful, policy-bearing, or extensible behavior to injected services where doing so improves testability and substitution. Keep genuinely pure constants/trivial value helpers static where DI would add ceremony without a useful seam.

## Network interface inventory backend

Replace the frontend mock interface inventory with an authoritative daemon-backed read path and ASP cache.

Target contract currently modeled by the client:

- `GET /api/v1/network-interfaces` returns the ASP-cached inventory.
- `POST /api/v1/network-interfaces/reconcile` forces ASP to refresh the inventory from the daemon and returns the refreshed snapshot.
- Response shape: `{ "interfaces": ["eno1", "docker0"], "reconciledAt": "<RFC 3339 timestamp>" }`.

The daemon read operation should enumerate known host network interfaces without requiring a signed mutation intent. ASP owns cache policy; the client treats the list as advisory autocomplete only, so free-text interface names remain valid and daemon-side rule validation remains authoritative.

Until this backend work is approved and implemented, `Ufw.Client` registers `MockNetworkInterfaceApiClient`. The real HTTP client is already implemented against the target contract so replacing the mock should require only DI/configuration wiring plus backend implementation and tests.

## Frontend internationalization (i18n)

Introduce a maintainable localization boundary for `Ufw.Client` before the final frontend polish pass. User-facing strings should come from localized resources rather than being embedded throughout Razor components and services, with English remaining the default/fallback culture.

The implementation should cover:

- choose and document the Blazor localization/resource organization so shared component strings, feature-specific strings, validation messages, dialogs, and navigation labels have clear ownership;
- provide a culture-selection surface, most naturally under Settings, and persist the user's choice across sessions;
- initialize the effective culture early enough that first render does not briefly show the fallback language;
- use culture-aware formatting for dates/times, counts, and other user-visible values while leaving protocol payloads, canonical UFW command syntax, identifiers, and signed structural data culture-invariant;
- preserve usable layouts for longer translations and include translated-string expansion in the final responsive/accessibility pass;
- define the fallback behavior for missing resources and keep adding a new language independent from application/business logic.

Do not translate protocol names, CLI syntax, raw interface names, rule identifiers, or other machine-facing values merely for presentation consistency.
