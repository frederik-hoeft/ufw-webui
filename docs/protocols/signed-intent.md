# Signed mutation intent v1

Firewall mutations are authorized by a user-held ECDSA P-256 key, not by JWT
material or IPC reachability. `Ufw.Web` forwards the signed envelope. Only
`Ufw.Systemd` verifies it.

Read requests (`GET /api/v1/rules`) are unsigned.

## Envelope

```json
{
  "version": 1,
  "keyId": "sha256:<base64url SPKI digest>",
  "issuedAtUnix": 1711972800,
  "nonce": "<base64url 16+ random bytes>",
  "operation": "rules.add",
  "payload": { },
  "signature": "<base64url IEEE P1363 ECDSA-SHA256>"
}
```

`operation` is `rules.add` or `rules.delete`. Future mutation types reuse this
envelope and add a payload canonicalizer.

### Add payload

```json
{
  "rule": {
    "action": "allow",
    "direction": "in",
    "protocol": "tcp",
    "source": "any",
    "sourcePorts": null,
    "sourceInterface": null,
    "destination": "any",
    "destinationPorts": "22",
    "destinationInterface": "eth0",
    "comment": "ssh"
  }
}
```

### Delete payload

```json
{
  "ruleId": "sha256:<content hash>",
  "rule": { }
}
```

`ruleId` must equal the identity computed from `rule`. The daemon rejects a
mismatch rather than trusting either field alone.

## Canonical bytes

The signature covers UTF-8 text, not JSON. After normalizing the rule
(`Anywhere` / `0.0.0.0/0` → `any`, sorted port lists, trimmed optionals):

```
ufw-intent/1
keyId=...
issuedAtUnix=...
nonce=...
operation=...
payload:
[ruleId=... only for delete]
action=allow
comment=...
destination=any
destinationInterface=...
destinationPorts=...
direction=in
protocol=tcp
source=any
sourceInterface=...
sourcePorts=...
```

ECDSA P-256 with SHA-256, IEEE P1363 (`r || s`). This matches WebCrypto.

`IntentRequestFactory` and `IntentSigner` in `Ufw.Ipc.Shared` implement this
encoding for tests and a future Blazor client.

## Rule identity

Identity is SHA-256 of the semantic fields only (action, direction, protocol,
source/destination address, ports, interfaces). Comments and UFW row numbers are
not part of identity. Semantically identical rules are the same rule.

List responses include `ruleId`, the current display number, parsed fields, and
the raw UFW line. Unparsed rows are returned with `parsed: false` and no
`ruleId`.

## Daemon checks

1. Envelope completeness and operation match
2. Payload schema and allowlisted field values
3. Identity match for delete
4. Signature against the local authorized-keys PEM file
5. Timestamp within `max_intent_age` and `clock_skew`
6. Under the UFW execution lock: consume nonce, re-read UFW, apply domain
   checks, execute `ufw`

Add rejects an existing identical rule. Delete rejects zero matches or more than
one match. The delete `ufw` argv uses the number from that locked re-list, never
a number supplied by the client.

Rule strings are passed only as validated argv elements. They are never
interpolated into a shell command.

## Operator configuration

```json
"security": {
  "authorized_keys_path": "/etc/ufw-manager/authorized_keys",
  "nonce_store_path": "/var/lib/ufw-manager/intent-nonces",
  "max_intent_age": "00:05:00",
  "clock_skew": "00:00:30"
}
```

Authorized keys file: one or more PEM public keys, `#` comments allowed.

```bash
openssl ecparam -name prime256v1 -genkey -noout -out intent-key.pem
openssl ec -in intent-key.pem -pubout -out intent-key.pub.pem
```
