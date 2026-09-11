# Signed Mutation Intent v2

Signed-intent v2 authorizes privileged firewall mutations independently of HTTP JWT state and IPC peer identity. A signing client creates the envelope; `Ufw.Web` forwards it; `Ufw.Systemd` reconstructs the canonical bytes and verifies them against daemon-owned trust state before any privileged UFW mutation can begin.

This document defines the project contract for `rules.add` and `rules.delete`. The requirement words describe interoperability and security requirements for UFW WebUI implementations.

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

Add and delete use the same outer envelope:

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
| `operation` | MUST be `rules.add` or `rules.delete` for the mutation endpoints defined by this protocol version |
| `payload` | operation-specific payload defined below |
| `signature` | base64url ECDSA P-256/SHA-256 signature in IEEE P1363 `r || s` form |

`keyId` is the string `sha256:` followed by the base64url-encoded SHA-256 digest of the signer's SubjectPublicKeyInfo. The corresponding public key MUST be present in the daemon's authorized-key store.

A future mutation operation MAY reuse the envelope only after defining its own canonical payload semantics. Unknown operations MUST be rejected.

## Rule specification

Both operations sign a normalized structural firewall rule. The JSON representation follows the shared protocol serializer, for example:

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

The signature does not cover this JSON text directly. The daemon validates and normalizes the semantic values and rebuilds the canonical signed bytes defined below.

A family-neutral rule is allowed for add when the rule semantics do not force IPv4 or IPv6. UFW may materialize that add as separate concrete family rows. Delete MUST carry a concrete IPv4 or IPv6 rule.

Interface fields follow UFW direction semantics. Inbound rules may use the inbound/destination-side interface, outbound rules may use the outbound/source-side interface, and forward rules may use both ingress and egress interfaces. Ambiguous combinations that would require precedence or fallback interpretation MUST be rejected.

## Operation payloads

### `rules.add`

```json
{
  "rule": { }
}
```

The daemon MUST normalize and validate the rule. Under the execution gate it MUST reject a currently observed semantically identical rule before starting UFW.

### `rules.delete`

```json
{
  "ruleId": "sha256:<semantic content hash>",
  "rule": { }
}
```

The rule MUST have a concrete address family. `ruleId` MUST equal the daemon-computed identity of the normalized `rule`. The daemon MUST reject a mismatch rather than trusting either field independently.

At execution time the daemon resolves that semantic identity against a fresh UFW snapshot and requires exactly one current match.

## Normalization

Before canonical signing bytes or semantic identity are produced, rule values are normalized according to the shared firewall model. At minimum:

- blank, `Anywhere`, and all-addresses forms normalize to `any`;
- IPv4 and IPv6 CIDRs normalize to their canonical network address and prefix;
- concrete addresses constrain address family consistently;
- port lists/ranges are sorted, deduplicated, and merged when overlapping or adjacent;
- interfaces and comments are trimmed;
- invalid direction/interface, family, protocol, address, or port combinations are rejected.

Both signing and verification MUST use the same normalization rules. A daemon MUST verify the semantic payload before computing canonical bytes.

## Canonical signed bytes

The v2 signature covers UTF-8 text with fixed field names, fixed field order, and normalized lower-case semantic values. JSON property ordering and whitespace are not signature inputs.

For add, the canonical form is:

```text
ufw-intent/2
deploymentId=<deployment id>
keyId=<key id>
issuedAtUnix=<unix seconds>
nonce=<nonce>
operation=rules.add
payload:
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

Delete uses the same form but inserts `ruleId=<normalized semantic identity>` immediately after `payload:`.

Each line ends with LF (`0x0A`) in the canonical byte sequence. Implementations MUST NOT substitute platform-specific line endings.

The signature algorithm is ECDSA over P-256 with SHA-256. The signature value uses the fixed-width IEEE P1363 concatenation of `r` and `s`, then base64url encoding in the JSON envelope.

## Semantic rule identity

Rule identity uses a separate canonical domain, `rule-identity/2`, and is the base64url-encoded SHA-256 digest of this UTF-8 representation:

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

The daemon assigns a mutable `ruleId` only to rows that it can parse completely and validate as supported semantics. Opaque or malformed rows remain visible through rule listing but MUST NOT be addressable by delete.

Delete MUST NOT accept a UFW display number as the durable mutation target. Under the execution gate, the daemon re-lists current state, resolves the signed semantic identity, and uses the current display number only as the final subprocess argument after unique resolution succeeds.

## Verification requirements

Before entering the privileged mutation boundary, the daemon verifies:

1. intent version and required fields;
2. deployment identity;
3. operation and endpoint agreement;
4. nonce encoding and minimum size;
5. operation payload shape;
6. rule normalization and semantic validation;
7. delete identity consistency, when applicable;
8. signature against the daemon-local authorized-key set;
9. issuance time against configured clock skew and maximum age.

A timestamp too far in the future MUST be rejected. An expired intent MUST be rejected.

Intent validity ends at the half-open boundary:

```text
issuedAtUnix + max_intent_age + clock_skew
```

## Replay protection and execution boundary

Signature verification alone does not make an intent reusable. After successful verification, mutation execution is serialized under the daemon's UFW execution gate.

Before a UFW child process can start, the daemon MUST durably consume the nonce. Replay state is persisted across daemon restart and retained until the same expiry boundary used for freshness validation. If replay state cannot be read or persisted safely, the daemon MUST fail closed.

After nonce consumption, the daemon:

1. reads authoritative UFW state;
2. performs operation-specific duplicate or target-resolution checks;
3. renders validated argv and executes UFW without a shell;
4. retains ownership of the child through completion or cancellation cleanup;
5. re-reads UFW and confirms the expected semantic postcondition before returning success.

A still-valid intent therefore cannot be accepted twice through sequential replay, concurrent submission, or daemon restart.

## Authorized keys and operator state

The daemon authorized-key file may contain comments and one or more ECDSA P-256 `PUBLIC KEY` PEM blocks. Private-key PEM blocks and unsupported key types MUST be rejected.

A signing key can be generated with:

```bash
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out intent-key.pem
openssl pkey -in intent-key.pem -pubout -out intent-key.pub.pem
```

The private key belongs to the signing client. Only the public key belongs on the daemon.

Paths for authorized keys, nonce state, deployment identity, and freshness policy are deployment configuration rather than wire fields. See [Deployment configuration](../deployment/configuration.md).
