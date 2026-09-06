# Open tasks

This file tracks only open project work. Remove an item when the corresponding work is completed and merged into the approved baseline.

## Firewall rule ordering

Design and implement ordered rule insertion and reordering across the browser, REST API, signed-intent protocol, daemon, and UFW subprocess boundary. The frontend must not imply drag/drop or insertion semantics until the mutation contract defines authoritative ordering, conflict behavior, stale-state handling, and signed authorization for the operation.

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

## Operational status and settings surfaces

Define the real data/contracts required for the remaining design-level operational navigation such as daemon connection/status and settings before exposing those pages in the frontend. Do not render fake connectivity or configuration state merely to match the visual reference.
