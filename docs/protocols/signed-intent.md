# Signed mutation intent v2

The signed-intent protocol authorizes privileged firewall mutations independently of JWT state and IPC peer identity. `Ufw.Web` forwards signed envelopes, while `Ufw.Systemd` reconstructs the canonical bytes and verifies them against daemon-owned trust state.

Rule listing and intent-context reads are unsigned at the mutation-protocol layer. They remain subject to the surrounding HTTP/API authorization policy when accessed through `Ufw.Web`.

## Intent context

A client obtains the current signing context from:

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

`deploymentId` is a stable random identifier persisted by the daemon. It scopes signatures to one daemon deployment and must be copied into the signed envelope exactly as returned.

## Envelope

AddRule and DeleteRule use the same envelope fields:

```json
{
  "version": 2,
  "deploymentId": "<daemon deployment id>",
  "keyId": "sha256:<base64url SPKI digest>",
  "issuedAtUnix": 1711972800,
  "nonce": "<base64url 16+ random bytes>",
  "operation": "rules.add",
  "payload": { },
  "signature": "<base64url IEEE P1363 ECDSA-SHA256>"
}
```

`operation` is currently `rules.add` or `rules.delete`. Future mutation types can reuse the envelope but require a canonical payload definition before they can be authorized safely.

`keyId` is `sha256:` followed by the base64url-encoded SHA-256 digest of the signer's SubjectPublicKeyInfo. The daemon authorizes the corresponding ECDSA P-256 public key from its local authorized-key file.

## Rule specification

The canonical rule specification contains these semantic fields:

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

The JSON enum names follow the source-generated protocol serializer. Signatures do not cover this JSON text; the daemon normalizes the semantic values and reconstructs the canonical field representation described below.

`addressFamily` may be family-neutral for AddRule. A family-neutral add can materialize as separate concrete IPv4 and IPv6 rows in UFW. Listed rules use concrete IPv4/IPv6 families, and DeleteRule requires that concrete family-specific specification.

Interface meaning follows UFW direction semantics: inbound rules may specify the inbound/destination-side interface, outbound rules the outbound/source-side interface, and forward rules may specify both ingress and egress interfaces. Combinations that would require fallback or precedence interpretation are rejected.

### Add payload

```json
{
  "rule": { }
}
```

The signed rule is normalized and checked for a semantically identical current rule before UFW execution.

### Delete payload

```json
{
  "ruleId": "sha256:<semantic content hash>",
  "rule": { }
}
```

`ruleId` must equal the daemon-computed identity of the supplied normalized rule. The rule must have a concrete IPv4 or IPv6 family. The daemon rejects a mismatch rather than trusting either field alone.

## Canonical signed bytes

The signature covers UTF-8 text with a fixed field order. JSON property order, JSON whitespace, and JSON spelling choices are not signed inputs.

For v2 the canonical representation is:

```text
ufw-intent/2
deploymentId=...
keyId=...
issuedAtUnix=...
nonce=...
operation=...
payload:
[ruleId=... only for delete]
action=allow
addressFamily=ipv4
comment=ssh
destination=any
destinationInterface=eth0
destinationPorts=22
direction=in
protocol=tcp
source=any
sourceInterface=
sourcePorts=
```

Before this representation is built, rule semantics are normalized:

- blank/`Anywhere`/all-addresses forms normalize to `any`;
- IPv4 and IPv6 CIDRs normalize to their canonical network address and prefix;
- address family is resolved consistently with concrete source/destination addresses;
- port lists/ranges are sorted, deduplicated, and merged when overlapping or adjacent;
- interfaces and comments are trimmed;
- invalid direction/interface, address-family, protocol, address, or port combinations are rejected.

Signatures use ECDSA P-256 with SHA-256 and IEEE P1363 fixed-field concatenation (`r || s`). `IntentRequestFactory`, `IntentCanonicalizer`, and `IntentSigner` in `Ufw.Ipc.Shared` implement the shared canonicalization/signing contract used by tests and future clients.

## Rule identity

Rule identity has its own versioned canonical domain (`rule-identity/2`) and is the SHA-256 hash of normalized firewall semantics:

- action;
- address family;
- direction;
- protocol;
- source/destination addresses;
- source/destination ports;
- directionally meaningful interfaces.

Comments and UFW display numbers are excluded. Equivalent supported textual forms therefore map to the same semantic identity, while IPv4 and IPv6 rows remain distinct.

`GET /api/v1/rules` returns the semantic `ruleId`, parsed specification, current display number, and raw UFW line for supported rows. A row receives a `ruleId` only when the parser consumes the complete row and the resulting semantic model passes mutation validation. Unsupported or malformed rows remain visible with no mutable identity.

DeleteRule never treats the display number as a stable address. Under the UFW execution gate, the daemon re-lists current state, resolves the signed semantic identity, requires exactly one current match, and only then uses that match's current UFW number for the subprocess call.

## Verification and execution lifecycle

The daemon processes a mutation in two stages.

Before entering the privileged mutation boundary it verifies:

1. intent protocol version and required envelope fields;
2. deployment identity;
3. operation/endpoint match;
4. nonce encoding and minimum size;
5. payload shape and semantic rule validation;
6. normalization and DeleteRule identity match;
7. signature against the daemon-local authorized-key set;
8. issued-at freshness and clock-skew limits.

Intent validity ends at the half-open boundary:

```text
issuedAtUnix + max_intent_age + clock_skew
```

A timestamp too far in the future is also rejected using the configured `clock_skew`.

After verification, mutation execution is serialized under the daemon UFW execution gate:

1. durably consume the nonce;
2. read authoritative UFW state;
3. apply duplicate or delete-target checks;
4. construct validated UFW argv and execute the child process without a shell;
5. retain ownership of the child through exit or cancellation cleanup;
6. re-read UFW and confirm the expected semantic postcondition before returning success.

The replay store retains a consumed nonce until the same expiry boundary used by intent validation and is persisted across daemon restarts. Corrupt or unwritable replay state fails closed. Consequently, a still-valid signed intent cannot be accepted twice through sequential replay, concurrent submission, or daemon restart.

## Operator configuration

Daemon intent security is configured under `security`:

```json
{
  "security": {
    "authorized_keys_path": "/etc/ufw-manager/authorized_keys",
    "nonce_store_path": "/var/lib/ufw-manager/intent-nonces",
    "deployment_id_path": "/var/lib/ufw-manager/deployment-id",
    "max_intent_age": "00:05:00",
    "clock_skew": "00:00:30"
  }
}
```

The authorized-key file may contain comments and one or more ECDSA P-256 `PUBLIC KEY` PEM blocks. Private-key PEM blocks and unsupported key types are rejected.

A test keypair can be generated with:

```bash
openssl ecparam -name prime256v1 -genkey -noout -out intent-key.pem
openssl ec -in intent-key.pem -pubout -out intent-key.pub.pem
```

The private key belongs to the signing client. Only the public-key PEM belongs in the daemon authorized-key file.
