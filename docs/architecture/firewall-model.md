# Firewall State and Rule Model

UFW WebUI treats UFW itself as the authoritative firewall database. The application does not attempt to mirror every rule into PostgreSQL or assume that it is the only actor modifying the firewall. This allows normal UFW tooling and other administrators to coexist with the web interface without creating two competing sources of truth.

The consequence is that every mutable rule must be addressable from current observed firewall semantics rather than from application-generated row numbers or database identifiers.

## Authoritative snapshots

Rule listing reads `ufw status numbered` through the privileged daemon. The daemon runs UFW under a deterministic locale, keeps parseable stdout separate from stderr diagnostics, and parses the complete numbered listing into a snapshot.

A snapshot can contain two kinds of rows:

- **supported rows** are fully parsed and pass the same semantic validation required for mutations;
- **opaque rows** remain visible through their raw UFW representation but cannot be mutated by semantic identity.

This distinction is deliberate. Partial parser understanding is sufficient for observability, but it is not sufficient authority to delete or rewrite a firewall rule.

All UFW reads and writes pass through one daemon execution gate. A read therefore cannot observe an intermediate state from a daemon-managed compound operation, and a mutation can compare pre- and post-operation snapshots without another daemon request interleaving.

## Structural rule semantics

Supported rules are represented by normalized firewall semantics rather than by the exact text UFW happened to print. The model contains:

- action: allow, deny, reject, or limit;
- address family;
- direction: inbound, outbound, or forward;
- protocol;
- source and destination addresses;
- source and destination ports;
- directionally meaningful interfaces;
- an optional comment.

Normalization removes textual differences that do not change firewall meaning. All-address forms become `any`, network addresses are canonicalized to their CIDR network boundary, port sets are sorted and merged, and textual fields are trimmed. Direction and interface combinations that would require fallback or precedence interpretation are rejected instead of guessed.

The same normalized model is used for validation, signed intents, semantic identity, duplicate detection, and UFW command rendering. This prevents the user-visible rule, the signed rule, and the executed argv from drifting into separate interpretations.

## Semantic identity

A supported listed rule receives a versioned SHA-256 identity derived from its normalized firewall semantics. Current UFW list numbers and comments are excluded.

The identity exists to answer one question: "which currently observed semantic rule is this?" It is not a persistent application record ID. Equivalent supported textual representations produce the same identity, while IPv4 and IPv6 rules remain distinct.

Delete requests carry both the semantic identity and the complete normalized rule specification. The daemon recomputes the identity from the supplied rule and rejects a mismatch. Under the execution gate it then takes a fresh UFW snapshot, finds current matches, and requires exactly one. Only after that resolution does it use the current UFW display number for the actual delete command.

This protects deletion from ordinary UFW renumbering and from stale browser snapshots. If no current rule matches, or more than one indistinguishable rule matches, the daemon refuses to guess.

## Address-family materialization

Add requests may be family-neutral when their semantic fields do not force IPv4 or IPv6. UFW can materialize such a request into concrete family-specific rows. The daemon therefore reasons about the observable concrete identities that can result from one structural add.

Listed rules and delete requests are always family-specific. Deleting a concrete IPv4 row does not implicitly delete a separate IPv6 row with otherwise similar semantics.

## Add lifecycle

An accepted add operation follows a conservative sequence:

1. verify the signed intent and enter the daemon execution gate;
2. durably consume the nonce;
3. verify that referenced host interfaces currently exist;
4. read current UFW state and reject a semantically identical existing rule;
5. render validated argv and start UFW directly, without a shell;
6. retain ownership of the child process through normal exit or cancellation cleanup;
7. read UFW again and require the expected semantic rule to be observable uniquely;
8. report success only after that postcondition is confirmed.

A zero child-process exit code is therefore necessary but not sufficient for success. If UFW reports success but the authoritative post-state cannot be reconciled safely, the daemon reports uncertainty/failure rather than inventing a confirmed rule state.

## Ordered insertion lifecycle

Ordered insertion changes membership and placement together, so it is authorized independently from append-style add and reorder. The browser signs the normalized new rule, the fingerprint of the exact authoritative snapshot being reviewed, one zero-based snapshot-local anchor occurrence, and whether the new rule belongs before or after that anchor. Duplicate semantic anchor rows remain independently addressable because occurrence identity is meaningful only inside the signed snapshot.

The inserted rule must have the same concrete IPv4 or IPv6 family as the parsed anchor. Family-neutral ordered creation is intentionally rejected because one UFW command could materialize into multiple concrete rows while one signed anchor identifies only one concrete ordered position. Ordinary append-style add retains family-neutral UFW behavior.

Under the execution gate, the daemon resolves any outstanding reorder recovery obligation, consumes the nonce, re-reads UFW, and requires the current snapshot fingerprint to equal the signed baseline before interpreting the anchor. Referenced interfaces and duplicate rule semantics are validated using the same authority as append add. `before` targets the anchor position. `after` targets the next occurrence in the same address-family partition, or appends within that concrete family when the anchor is the last occurrence in its partition.

Snapshot occurrences use the combined UFW listing for authorization, but UFW interprets `insert N` within the concrete address-family partition. The daemon translates the signed combined-list anchor into a family-local UFW insertion position immediately before command construction. It then executes one insertion and reconciles the complete post-state. Success requires every baseline occurrence to remain in relative order, exactly one requested rule materialization to have been added, and that row to occupy the signed slot. No recovery journal is needed because ordered insertion never removes an existing row.

A verified insertion returns a typed result with the final authoritative snapshot whenever it can be read safely. `Ufw.Web` maps completed execution to HTTP 200, a stale baseline to 409, a precondition failure to 422, and uncertain authoritative state to 503 while preserving the typed report body. Signature, replay, and malformed-intent failures use the normal API error representation.

## Delete lifecycle

Delete follows the same authorization, nonce, process-ownership, and reconciliation rules, with target resolution replacing duplicate detection:

1. take a current snapshot under the execution gate;
2. resolve the signed semantic identity to exactly one current row;
3. use that row's current UFW number for the delete subprocess;
4. re-read UFW and require the semantic identity to be absent before returning success.

Interface existence is intentionally not revalidated for delete. A rule referencing an interface that has since disappeared must still be removable.

## Reorder lifecycle

Reordering is a state-conditioned mutation over one exact authoritative snapshot. The browser computes a versioned SHA-256 fingerprint from the complete ordered `RuleListResponse` it displays and assigns each row a snapshot-local occurrence ID equal to its zero-based position. The signed request binds that fingerprint and the complete desired occurrence permutation. Occurrence IDs are intentionally local to the fingerprinted snapshot; they distinguish duplicate semantic rules without pretending to be durable rule identities.

Under the execution gate, the daemon re-lists UFW and requires the current fingerprint to equal the signed baseline before any reorder mutation begins. It validates that the desired order is a complete permutation, that concrete IPv4 rows remain before IPv6 rows, and that every occurrence which must move can be rendered losslessly for reinsertion. Opaque or otherwise non-reinsertable rows remain fixed anchors.

For a valid baseline the planner reduces the permutation to a longest-increasing-subsequence problem. Rows in the selected subsequence remain untouched; every other movable occurrence is deleted and reinserted once, producing a globally minimal number of logical moves under the unit-cost remove/reinsert model. Safety weights choose which rows remain untouched when multiple equally minimal plans exist.

UFW exposes one combined numbered status sequence but interprets `insert N` within the concrete address-family partition of the rule being inserted. The daemon therefore keeps snapshot occurrence IDs in combined-list coordinates for authorization and planning, then translates an insertion anchor into a family-local UFW position immediately before command construction. Opaque rows still participate in that position calculation; the `(v6)` marker in UFW status output supplies their observed family when no parsed structural rule is available.

Each logical move is a recovery unit. Immediately before deleting a row, the daemon re-reads UFW to ensure the expected occurrence order still holds and persists a recovery record containing the rule needed to restore that row. Once deletion may have taken effect, caller cancellation or a changed reorder plan cannot abandon the row: the daemon reconciles authoritative state and either performs the planned insertion or best-effort reinserts the removed rule before stopping. The recovery record is cleared only after row presence is confirmed. Startup and every later firewall mutation first resolve any outstanding recovery record or fail closed.

A reorder response is a transaction report. It carries the final authoritative snapshot whenever that state can be read safely, the operations that were applied or recovered, the unapplied suffix of the reviewed plan, and a freshly derived residual plan only when the final state can still be mapped unambiguously to the signed baseline occurrences. Pending operations are diagnostic; continuing always requires a fresh authoritative snapshot and a newly signed request. `Ufw.Web` preserves that report body while mapping `Completed` to HTTP 200, stale or partial outcomes to 409, precondition failure to 422, recovery failure to 500, and uncertain authoritative state to 503. Signature, replay, and malformed-intent failures use the normal API error representation instead.

This is transaction-level recovery around sequential UFW commands, not packet-level atomic replacement of the firewall ruleset. Packets can observe the short intermediate state between deletion and reinsertion, and an unrelated process invoking UFW can still race the daemon.

## Cancellation and uncertain outcomes

Once a UFW mutation process starts, caller cancellation cannot safely mean "the mutation did not happen." The daemon therefore keeps process ownership, terminates and reaps the child if required, and performs an authoritative reconciliation read before propagating cancellation. During reordering, a delete/reinsert recovery obligation continues independently of caller cancellation until row presence is confirmed or the daemon must fail closed with the durable recovery record intact.

If reconciliation cannot establish the postcondition, callers must treat their previous snapshot as stale and refresh before attempting another mutation. The browser follows that rule and disables further mutation while its displayed snapshot is known to be stale.

## Host interfaces and application metadata

Host interface existence is a property of the operating system, observed by the daemon. `Ufw.Web` maintains a separate reconciled catalog only to attach application metadata such as comments and "show in suggestions" visibility.

Reconciliation preserves metadata for interface names that still exist, creates entries for new names, and removes entries whose host interface disappeared. Hiding an entry changes only the rule-authoring UI. It does not make the interface invalid, and a stale cached entry cannot authorize an add or ordered insertion because the daemon checks the signed interface name against a fresh host snapshot before execution.

This pattern is the intended model for future authoring metadata as well: application-owned names, comments, or search aids may improve usability, but the signed and executed rule must resolve entirely to firewall semantics understood by the daemon.

## Out-of-band changes

Administrators and other tools may change UFW outside UFW WebUI. The architecture expects this rather than treating it as corruption.

A subsequent list observes those changes directly. Supported externally-created rules receive the same semantic identities as equivalent rules created through the web interface. Renumbering does not break identity. Unsupported syntax remains observable but read-only.

The daemon serializes only its own UFW accesses. It does not provide a cross-process lock against an administrator or unrelated program invoking UFW concurrently, so a simultaneous external mutation can still create an unavoidable host-level race. For state-conditioned insertion or reorder, a baseline mismatch before the first subprocess is reported without mutation. If ordered insertion observes anything other than its exact expected post-state after the subprocess may have run, it reports authoritative uncertainty rather than attempting a compensating mutation. Reorder divergence stops further planned moves after the active row has been made safe, and its transaction report describes the authoritative state and any safely derivable remaining work.

## Mutation boundary

The privileged mutation contract supports append-style add, exact-snapshot ordered insertion, semantic delete, and exact-snapshot reorder. Ordered insertion changes membership and placement together, so it remains a distinct signed operation rather than an extension of append add or reorder.
