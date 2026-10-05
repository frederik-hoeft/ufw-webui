# Signed Mutation Intent v2

Signed-intent v2 authorizes privileged firewall mutations independently of HTTP JWT state and IPC peer identity. A signing client creates the envelope; `Ufw.Web` validates the exact REST envelope shape/format and state-independent canonical payload semantics as a defense-in-depth filter, then copies the signed values unchanged into the daemon IPC request; `Ufw.Systemd` reconstructs the canonical bytes and performs the authoritative verification against daemon-owned trust state before any privileged UFW mutation can begin.

This document defines the project contract for `rules.add`, `rules.insert`, `rules.replace`, `rules.delete`, `rules.delete-batch`, and `rules.reorder`. The requirement words describe interoperability and security requirements for UFWeb implementations.

Read-only rule listing, network-interface discovery, and intent-context retrieval are unsigned at this protocol layer. They may still require authentication at surrounding layers.

## Intent context

Before signing, a client obtains the daemon context from the authenticated REST endpoint:

```text
GET /api/v1/intent/context
```

The response contains:

```json
{
  "protocolVersion": 2,
  "deploymentId": "<base64url daemon deployment id>"
}
```

`deploymentId` is a stable random identifier persisted by the daemon. A client MUST copy it exactly into the signed envelope. A daemon MUST reject an intent whose deployment identifier does not match its own current deployment.

## Envelope

All defined mutation operations use the same outer envelope:

```json
{
  "version": 2,
  "deploymentId": "<daemon deployment id>",
  "keyId": "sha256:<base64url SPKI digest>",
  "issuedAtUnix": 1711972800,
  "nonce": "<base64url random bytes>",
  "operation": "rules.add",
  "payload": {},
  "signature": "<base64url IEEE-P1363 ECDSA signature>"
}
```

| Field | Requirement |
| --- | --- |
| `version` | MUST equal `2` |
| `deploymentId` | MUST match the current daemon intent context |
| `keyId` | MUST identify an authorized P-256 public key |
| `issuedAtUnix` | Unix timestamp used for freshness validation |
| `nonce` | base64url random value decoding to at least 16 bytes; the project signer emits 16 bytes |
| `operation` | MUST be `rules.add`, `rules.insert`, `rules.replace`, `rules.delete`, `rules.delete-batch`, or `rules.reorder` for the mutation endpoints defined here |
| `payload` | operation-specific payload defined below |
| `signature` | base64url ECDSA P-256/SHA-256 signature in IEEE P1363 `r || s` form |

`keyId` is the string `sha256:` followed by the base64url-encoded SHA-256 digest of the signer's SubjectPublicKeyInfo. The corresponding public key MUST be present in the daemon's authorized-key store.

An additional mutation operation MAY reuse the v2 envelope after defining its own canonical payload semantics. Adding such an operation is an operation-set extension and does not by itself change the signed-intent protocol version. The global signed-intent version is reserved for incompatible changes to the shared envelope, canonicalization domain, or verification semantics that apply across the signed operation set. Unknown operations MUST be rejected.

## Rule specification

Add, ordered insertion, replacement, and delete sign a normalized structural firewall rule. The JSON representation follows the shared protocol serializer, for example:

```json
{
  "action": "Allow",
  "addressFamily": "IPv4",
  "direction": "In",
  "protocol": "Tcp",
  "source": "any",
  "sourcePorts": null,
  "sourceInterface": null,
  "destination": "any",
  "destinationPorts": "22",
  "destinationInterface": "eth0",
  "comment": "ssh"
}
```

The signature does not cover this JSON text directly. `Ufw.Web` nevertheless requires the REST payload to already use the canonical, state-independent representation expected from the signing client and rejects malformed, unknown, duplicate, semantically invalid, or non-canonical values before privileged IPC. This validation is non-mutating: ASP may deserialize a copy for inspection, but it MUST copy the original signed envelope fields and payload into the daemon request without normalizing, rewriting, or reserializing them.

The daemon remains authoritative. It independently binds the forwarded payload, validates and normalizes its semantic values, reconstructs the canonical signed bytes defined below, verifies the signature and trust state, and applies all freshness, replay, current-state, and execution preconditions. ASP-side rejection is defense in depth and MUST NOT replace or weaken daemon-side validation.

A family-neutral rule is allowed for append add when the rule semantics do not force IPv4 or IPv6. UFW materializes that add as separate concrete family rows when IPv6 is enabled and as IPv4 only when IPv6 is disabled. Ordered insertion, replacement, and delete MUST carry a concrete IPv4 or IPv6 rule.

Interface fields follow UFW direction semantics. Inbound rules may use the inbound/destination-side interface, outbound rules may use the outbound/source-side interface, and forward rules may use both ingress and egress interfaces. Ambiguous combinations that would require precedence or fallback interpretation MUST be rejected.

## Operation payloads

### `rules.add`

```json
{
  "rule": { }
}
```

The daemon MUST normalize and validate the rule. Under the execution gate it MUST reject a currently observed semantically identical rule before starting UFW.

### `rules.insert`

```json
{
  "baselineFingerprint": "sha256:<base64url snapshot digest>",
  "anchorOccurrenceId": 3,
  "placement": "Before",
  "rule": { }
}
```

`baselineFingerprint` identifies the exact ordered firewall-list projection of the `RuleListResponse` reviewed by the signer. `anchorOccurrenceId` is the zero-based occurrence of the selected row in that baseline, and `placement` is `Before` or `After`. Occurrence IDs are snapshot-local rather than semantic identities, so duplicate semantic anchor rows remain independently addressable.

The inserted rule MUST have a concrete address family equal to the parsed anchor's concrete family. Family-neutral ordered insertion is rejected because one signed occurrence identifies one concrete ordered position while a family-neutral UFW add may materialize into multiple family-specific rows. Under the serialized execution boundary, the daemon MUST require a fresh authoritative snapshot matching `baselineFingerprint` before interpreting the occurrence.

### `rules.replace`

```json
{
  "baselineFingerprint": "sha256:<base64url snapshot digest>",
  "targetOccurrenceId": 3,
  "originalRuleId": "sha256:<semantic content hash>",
  "replacementRule": { }
}
```

`baselineFingerprint` identifies the exact authoritative ordered-list snapshot reviewed by the signer. `targetOccurrenceId` is the zero-based occurrence being edited inside that baseline, and `originalRuleId` is the semantic identity expected at that occurrence. `replacementRule` is the complete normalized structural rule proposed as the replacement.

The target and replacement MUST both have the same concrete IPv4 or IPv6 family. The daemon MUST require a fresh authoritative snapshot matching `baselineFingerprint`, resolve the signed occurrence, require its semantic identity to equal `originalRuleId`, and reject any unsafe duplicate-state precondition before a mutating UFW command begins. In particular, a same-identity existing-rule update requires the original semantic identity to occur exactly once, and an identity-changing replacement MUST be rejected if the replacement semantic identity already exists in the baseline.

### `rules.delete`

```json
{
  "ruleId": "sha256:<semantic content hash>",
  "rule": { }
}
```

The rule MUST have a concrete address family. `ruleId` MUST equal the daemon-computed identity of the normalized `rule`. The daemon MUST reject a mismatch rather than trusting either field independently.

At execution time the daemon resolves that semantic identity against a fresh UFW snapshot and requires exactly one current match.

### `rules.delete-batch`

```json
{
  "baselineFingerprint": "sha256:<base64url snapshot digest>",
  "occurrenceIds": [7, 3, 1]
}
```

`baselineFingerprint` identifies the exact authoritative ordered-list snapshot reviewed by the signer. `occurrenceIds` is a non-empty set of unique, non-negative zero-based occurrence IDs from that snapshot. Their order is part of the signed payload, although the daemon is free to choose an execution order that preserves the authorized target set; the project daemon deletes in descending baseline-occurrence order so ordinary UFW renumbering cannot retarget later selected occurrences.

Batch deletion addresses exact snapshot occurrences rather than semantic `RuleId`s so duplicate semantic rows remain independently selectable. The payload is intentionally generic and MUST NOT contain ASP-owned grouping identifiers or other application metadata. A higher-level workflow such as deleting all rules in a rule group resolves that application state to the exact occurrence set before signing.

Under the serialized execution boundary, the daemon MUST require a fresh authoritative snapshot matching `baselineFingerprint` before interpreting the occurrence IDs. Every selected occurrence MUST be inside the baseline and MUST have a usable UFW display number.

### `rules.reorder`

```json
{
  "baselineFingerprint": "sha256:<base64url snapshot digest>",
  "desiredOrder": [0, 3, 1, 2]
}
```

`baselineFingerprint` identifies the exact ordered firewall-list projection of the `RuleListResponse` reviewed by the signer. UFW's projection is deterministic but family-partitioned: IPv4 rows precede IPv6 rows, while first-match ordering is meaningful only within each family. `desiredOrder` contains zero-based occurrence IDs from that baseline and expresses the complete desired projection. Valid reorder requests preserve the IPv4-then-IPv6 partition and only change relative order within a family. Occurrence IDs are snapshot-local identifiers, not semantic rule IDs or durable UFW row numbers.

The daemon MUST reject a malformed fingerprint. After authorization and nonce consumption, reorder preflight MUST require the desired order to be a complete valid permutation of the authoritative baseline and MUST enforce all daemon-side moveability and address-family ordering constraints before starting a reorder mutation.

## Rule normalization

Before add/insert/replace/delete canonical signing bytes or semantic identity are produced, rule values are normalized according to the shared firewall model. At minimum:

- blank, `Anywhere`, and all-addresses forms normalize to `any`;
- IPv4 and IPv6 CIDRs normalize to their canonical network address and prefix;
- concrete addresses constrain address family consistently;
- port lists/ranges are sorted, deduplicated, and merged when overlapping or adjacent;
- interfaces and comments are trimmed;
- invalid direction/interface, family, protocol, address, or port combinations are rejected.

Both signing and verification MUST use the same normalization rules. A daemon MUST verify the semantic payload before computing canonical rule-operation bytes.

## Canonical signed bytes

The v2 signature covers UTF-8 text with fixed field names and fixed field order. JSON property ordering and whitespace are not signature inputs.

Every canonical intent starts with:

```text
ufw-intent/2
deploymentId=<deployment id>
keyId=<key id>
issuedAtUnix=<unix seconds>
nonce=<nonce>
operation=<operation>
payload:
```

For add, the payload continues with:

```text
action=<normalized action>
addressFamily=<normalized family>
comment=<normalized comment or empty>
destination=<normalized destination>
destinationInterface=<normalized interface or empty>
destinationPorts=<normalized ports or empty>
direction=<normalized direction>
protocol=<normalized protocol>
source=<normalized source>
sourceInterface=<normalized interface or empty>
sourcePorts=<normalized ports or empty>
```

Ordered insertion prefixes the same normalized rule fields with its exact placement context:

```text
baselineFingerprint=<snapshot fingerprint>
anchorOccurrenceId=<zero-based occurrence id>
placement=before|after
action=<normalized action>
addressFamily=<normalized concrete family>
comment=<normalized comment or empty>
destination=<normalized destination>
destinationInterface=<normalized interface or empty>
destinationPorts=<normalized ports or empty>
direction=<normalized direction>
protocol=<normalized protocol>
source=<normalized source>
sourceInterface=<normalized interface or empty>
sourcePorts=<normalized ports or empty>
```

Replacement prefixes the normalized replacement rule fields with the exact reviewed target context:

```text
baselineFingerprint=<snapshot fingerprint>
targetOccurrenceId=<zero-based occurrence id>
originalRuleId=<normalized semantic identity>
action=<normalized action>
addressFamily=<normalized concrete family>
comment=<normalized comment or empty>
destination=<normalized destination>
destinationInterface=<normalized interface or empty>
destinationPorts=<normalized ports or empty>
direction=<normalized direction>
protocol=<normalized protocol>
source=<normalized source>
sourceInterface=<normalized interface or empty>
sourcePorts=<normalized ports or empty>
```

Delete uses the same rule form as add but inserts this line immediately after `payload:`:

```text
ruleId=<normalized semantic identity>
```

Reorder instead continues with:

```text
baselineFingerprint=<snapshot fingerprint>
desiredOrderCount=<number of occurrences>
desiredOrder[0]=<occurrence id>
desiredOrder[1]=<occurrence id>
...
```

Batch delete continues with:

```text
baselineFingerprint=<snapshot fingerprint>
occurrenceIdCount=<number of selected occurrences>
occurrenceIds[0]=<occurrence id>
occurrenceIds[1]=<occurrence id>
...
```

Each line ends with LF (`0x0A`) in the canonical byte sequence. Implementations MUST NOT substitute platform-specific line endings.

The signature algorithm is ECDSA over P-256 with SHA-256. The signature value uses the fixed-width IEEE P1363 concatenation of `r` and `s`, then base64url encoding in the JSON envelope.

## Semantic rule identity

Semantic rule identity used by delete and replacement uses a separate canonical domain, `rule-identity/2`, and is the base64url-encoded SHA-256 digest of this UTF-8 representation:

```text
rule-identity/2
action=<normalized action>
addressFamily=<normalized concrete family>
destination=<normalized destination>
destinationInterface=<normalized interface or empty>
destinationPorts=<normalized ports or empty>
direction=<normalized direction>
protocol=<normalized protocol>
source=<normalized source>
sourceInterface=<normalized interface or empty>
sourcePorts=<normalized ports or empty>
```

Each line ends with LF. The externally visible identifier is `sha256:` followed by the base64url digest. Comments and UFW display numbers are excluded.

The daemon assigns a mutable `ruleId` only to rows that it can parse completely and validate as supported semantics. Opaque or malformed rows remain visible through rule listing but MUST NOT be addressable by delete or replacement.

Delete MUST NOT accept a UFW display number as the durable mutation target. Replacement likewise binds one occurrence only inside the fingerprinted snapshot rather than treating its current display number as durable identity. Under the execution gate, the daemon re-lists current state and derives any UFW display-number command argument only after the corresponding semantic/snapshot preconditions have been reconciled.

## Authoritative snapshot fingerprint

Ordered insertion, replacement, batch deletion, and reorder use a shared versioned fingerprint domain:

```text
ufw-webui/firewall-rule-snapshot/1
```

The fingerprint commits to the exact authoritative ordered-list representation displayed to the signer, not only to semantic rule identities. Operational configuration carried beside that list in `RuleListResponse` (currently IPv6 capability and default policies) is intentionally outside fingerprint version 1; mutation-specific capability checks use fresh daemon configuration independently. Its canonical binary input contains, in order:

1. the context string;
2. firewall active state;
3. rule count;
4. for every rule at its current zero-based occurrence index:
   - occurrence index;
   - nullable UFW display number;
   - parsed flag;
   - nullable raw row text;
   - nullable semantic `ruleId`;
   - presence of a structured rule;
   - when present, canonical formatted action, address family, direction, and protocol plus the snapshot's nullable source, source ports, source interface, destination, destination ports, destination interface, and comment fields.

Strings are encoded as a four-byte big-endian byte length followed by UTF-8 bytes. Integers are four-byte big-endian values. Booleans are one byte (`0` or `1`). Nullable fields are preceded by a boolean presence marker. The SHA-256 digest of this binary representation is base64url encoded and exposed as `sha256:<digest>`.

Because occurrence numbers are meaningful only inside this fingerprinted snapshot, semantically identical duplicate rows remain independently addressable where an operation can safely use occurrence identity, including insertion anchors, batch deletion, and reorder. Replacement also signs one exact occurrence, but its execution preconditions deliberately reject duplicate cases that UFW cannot update safely. A signer MUST compute the fingerprint from the exact authoritative ordered-list snapshot being presented for review; a fingerprint supplied independently by `Ufw.Web` would not bind the browser-visible rule order.

## Verification requirements

Before an intent is accepted for privileged execution, the daemon verifies:

1. intent version and required fields;
2. deployment identity;
3. operation and endpoint agreement;
4. nonce encoding and minimum size;
5. operation payload shape;
6. add/insert/replace/delete rule normalization and semantic validation, when applicable;
7. insertion fingerprint, occurrence, placement, and concrete-family payload shape, when applicable;
8. replacement fingerprint, target occurrence, original semantic identity, and concrete-family payload shape, when applicable;
9. delete identity consistency, when applicable;
10. batch-delete fingerprint syntax and non-empty unique occurrence selection, when applicable;
11. reorder fingerprint syntax and desired-order presence, when applicable;
12. signature against the daemon-local authorized-key set;
13. issuance time against configured clock skew and maximum age.

A timestamp too far in the future MUST be rejected. An expired intent MUST be rejected.

Intent validity ends at the half-open boundary:

```text
issuedAtUnix + max_intent_age + clock_skew
```

Insertion anchor validity/exact baseline equality, replacement target identity/family/duplicate safety/exact baseline equality, batch-delete occurrence validity/exact baseline equality, and reorder permutation validity/exact baseline equality/reinsertability/move planning depend on current authoritative state and are therefore checked inside the serialized execution boundary rather than during signature parsing.

## Replay protection and execution boundary

Signature verification alone does not make an intent reusable. After successful verification, mutation execution is serialized under the daemon's UFW execution gate.

Before a UFW child process can start, the daemon MUST durably consume the nonce. Replay state is persisted across daemon restart and retained until the same expiry boundary used for freshness validation. If replay state cannot be read or persisted safely, the daemon MUST fail closed.

Append add and delete then resolve their operation-specific preconditions, execute validated argv without a shell, retain ownership of the child through completion or cancellation cleanup, and re-read UFW before confirming success.

Ordered insertion requires its fresh authoritative snapshot to match `baselineFingerprint`, resolves the signed anchor occurrence, verifies concrete-family compatibility plus add-style interface/duplicate preconditions, and translates before/after placement into UFW's combined numbered `insert N` coordinate for that concrete family. It executes one UFW mutation and confirms success only if the complete authoritative post-state equals the expected baseline-plus-one-row state. Because it does not delete an existing row, it creates no insertion recovery journal. A verified insertion returns a typed result for completed, stale-baseline, precondition-failed, or state-uncertain outcomes; continuing after a stale or uncertain result requires a fresh baseline and new signature.

Replacement requires its fresh authoritative snapshot to match `baselineFingerprint`, resolves `targetOccurrenceId`, requires the target identity to equal `originalRuleId`, and enforces concrete-family, capability, interface, and duplicate-safety preconditions before mutation. If the replacement has the same semantic identity, an identical normalized rule is a no-op and a comment-only change uses UFW's existing-rule update behavior; this path is accepted only when the original semantic identity is unique. If the identity changes, the daemon inserts the replacement immediately before the original and requires the exact baseline-plus-one-row intermediate state before deleting the original. The requested operation is `Completed` only when the exact final replacement state is confirmed authoritatively.

If identity-changing deletion does not complete while the exact intermediate state is still confirmed, the daemon MUST attempt to remove the newly inserted replacement on a best-effort basis and reports whether the exact baseline was restored. It MUST NOT attempt compensating mutation from an unclassifiable state. Replacement recovery is synchronous and has no durable recovery journal; a daemon/process failure can therefore leave a safely classifiable partial state or an uncertain state that requires operator review and a fresh signed context. The typed response preserves the replacement outcome, optional recovery outcome, final snapshot when known, confirmed replacement row on completion, and diagnostic information.

Batch deletion additionally requires its fresh authoritative snapshot to match `baselineFingerprint` before the first delete. It validates the selected baseline occurrences and then processes them iteratively. Before every individual delete it re-reads UFW and requires the current rule sequence to equal the expected surviving subsequence of the signed baseline; after the subprocess it re-reads again and confirms that exactly the selected occurrence disappeared. A failed process can still be reported as a confirmed deletion when the authoritative post-state proves the deletion occurred, while a successful exit cannot override a mismatched or unreadable post-state. Drift, failed reconciliation, or uncertainty stops the batch. Already confirmed deletions are not speculatively reinserted.

A verified batch-delete response reports the overall outcome, every attempted occurrence, the still-pending signed occurrences, a diagnostic where relevant, and the final authoritative snapshot when one is safely known. `Completed`, `StaleBaseline`, `PreconditionFailed`, `PartiallyCompleted`, and `StateUncertain` are transaction outcomes rather than signature errors. Continuing after any non-completed result requires a fresh authoritative baseline and new signature.

Reorder additionally requires its fresh authoritative snapshot to match `baselineFingerprint`. It derives a minimal move plan from the signed occurrence permutation. Each logical move is reconciled against authoritative state, and a durable recovery record is persisted before a delete that may require reinsertion. Once such a delete may have taken effect, the daemon MUST confirm or restore row presence before it can safely abandon that move. An outstanding recovery record blocks later firewall mutations until recovery succeeds or the operator resolves the underlying state.

A verified reorder execution returns a structured transaction report even when the desired ordering was not fully reached. Signature, authorization, malformed-intent, and replay failures remain protocol/application errors rather than transaction outcomes. Pending reorder operations are diagnostic only and MUST NOT be treated as reusable authorization; continuing requires a new authoritative baseline, nonce, and signature.

A still-valid intent therefore cannot be accepted twice through sequential replay, concurrent submission, or daemon restart.

Replacement, batch deletion, and the delete/reinsert sequence used for reorder are not packet-level atomic. The signed-intent protocol authorizes the reviewed transition and the daemon provides serialization plus operation-specific reconciliation/recovery, but traffic can observe intermediate UFW policy between sequential CLI mutations.

## Authorized keys and operator state

The daemon authorized-key file may contain comments and one or more ECDSA P-256 `PUBLIC KEY` PEM blocks. Private-key PEM blocks and unsupported key types MUST be rejected.

A signing key can be generated with:

```bash
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out intent-key.pem
openssl pkey -in intent-key.pem -pubout -out intent-key.pub.pem
```

The private key belongs to the signing client. Only the public key belongs on the daemon.

Paths for authorized keys, nonce state, deployment identity, reorder recovery state, and freshness policy are deployment configuration rather than wire fields. See [Deployment configuration](../deployment/configuration.md).
