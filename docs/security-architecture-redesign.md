# UFW WebUI Security Architecture Redesign Draft

## 1. Purpose

This document describes a proposed redesign for the UFW WebUI project with a stronger privilege boundary, explicit write authorization, and daemon-owned firewall mutation logic.

The project goal is to provide a web-based management interface for UFW on a central firewall host in a multi-legged DMZ environment, without allowing compromise of the web application container to directly imply compromise of the host firewall configuration.

Core principle:

> The web UI is a convenience layer. The ASP.NET backend authenticates users and forwards requests. The privileged daemon is the policy authority. Firewall writes require explicit admin-signed, daemon-issued canonical intents.

## 2. Current Repository State

The existing project already has a useful high-level split:

- `Ufw.Web`: ASP.NET Core web application using Identity, EF Core, SQLite, and Razor Pages.
- `Ufw.Systemd`: host-side privileged service intended to run under systemd.
- `Ufw.Ipc.Shared`: shared IPC contracts, messages, serialization, and transport abstractions.
- `Ufw.Ipc.Client`: web-side IPC client library.
- `Ufw.Roslyn` / `Ufw.Roslyn.SourceGen`: source-generated controller-style endpoint mapping.
- `Ufw.Web.Tests`: tests for web-side behavior.

This is the right general direction: an unprivileged web app talks to a privileged daemon over a narrow IPC boundary.

However, the current implementation should be treated as a prototype, not as a deployable security boundary:

- IPC security is still incomplete; `NoTransportSecurityService` exists and is currently wired for testing.
- The implemented concrete transport is hardcoded loopback TCP on port `1234`, not a production Unix domain socket or named pipe boundary.
- The current architecture document describes named pipes and mutual TLS as intended, but the implementation has not yet reached that target.
- UFW command execution, daemon-side command planning, rollback handling, anti-lockout checks, and audit logging still need to be designed and implemented as first-class security features.

The revival work should therefore start with privilege-boundary hardening, not UI polish.

## 3. Security Goals

### 3.1 Privilege separation

The web application runs as an unprivileged containerized process. It must not have direct access to:

- `/usr/sbin/ufw`
- `/etc/ufw`
- host firewall APIs
- the Docker socket
- privileged Linux capabilities
- arbitrary host filesystems

Only the host daemon may mutate firewall state.

### 3.2 Backend compromise resistance

A compromised ASP.NET backend must not be able to forge firewall write operations by itself.

It may still be able to deny service, hide UI state, forward garbage to the daemon, or serve malicious frontend assets. The daemon design must assume all requests from ASP.NET are attacker-controlled unless cryptographically and semantically verified.

### 3.3 Explicit approval for writes

Every firewall mutation must require an admin signing key.

The final approval signature must cover a daemon-issued canonical change request, not merely a timestamp, session token, ASP.NET DTO, or raw command string.

### 3.4 Daemon authority

The daemon is authoritative for:

- rule canonicalization,
- validation,
- policy checks,
- interface existence checks,
- current firewall state,
- rule identity/version checks,
- anti-lockout checks,
- replay prevention,
- pending intent storage,
- UFW command construction,
- UFW execution,
- audit logging.

ASP.NET-side normalization is allowed for UX and type safety, but it is not trusted by the daemon.

## 4. Threat Model

The design should handle:

- unauthenticated clients reaching the web UI,
- authenticated low-privilege users,
- compromised ASP.NET backend,
- compromised web container filesystem,
- malicious or malformed draft requests,
- replayed signed requests,
- stale approvals after firewall state changed,
- stolen or revoked admin device keys,
- daemon restart while intents are pending,
- concurrent firewall mutations,
- accidental self-lockout.

The design cannot fully solve:

- a fully compromised admin device that can sign malicious approvals,
- a fully compromised host root account,
- malicious frontend code served by a compromised backend while a browser-held signing key is available.

Browser-side Web Crypto signing helps, but it is not equivalent to hardware-backed or external approval. Non-extractable browser keys reduce accidental key export, but malicious same-origin JavaScript can still ask the key to sign while it is available.

## 5. Target Component Architecture

```text
Admin device
  Browser / Blazor WebAssembly UI
    - drafts firewall changes
    - signs draft preflight envelopes
    - verifies daemon-signed previews
    - signs daemon-issued apply approvals

HTTPS

ASP.NET Core API container
  - non-root
  - Identity/OIDC authentication
  - session/RBAC checks
  - UI state and metadata
  - forwards signed requests
  - no direct UFW/host firewall access

Unix domain socket / local IPC

ufw-managerd privileged host daemon
  - daemon-side validation and policy
  - admin key registry
  - nonce/replay cache
  - pending intent store
  - command planner
  - single-writer firewall mutation lock
  - audit log

UFW / host firewall state
```

## 6. Trust Boundaries

### 6.1 Browser to ASP.NET API

Transport: HTTPS.

The ASP.NET API performs normal web authentication and authorization:

- secure cookies,
- CSRF protection for cookie-authenticated write endpoints,
- HSTS,
- strict CSP,
- rate limiting,
- login hardening,
- optional MFA.

Browser-side signing is not a substitute for web authentication. It is an additional approval mechanism for privileged firewall changes.

### 6.2 ASP.NET API to daemon

Transport: preferably Unix domain socket on Linux.

Recommended path:

```text
/run/ufw-manager/api.sock
```

Only this socket should be mounted into the web container. The daemon should authenticate the peer through one or more of:

- Unix socket filesystem permissions,
- peer credentials where available,
- mTLS over the socket stream,
- explicit client certificate allowlist.

This authenticates the web service as a forwarding component. It does not authorize firewall writes by itself.

### 6.3 Daemon to UFW

Only the daemon executes UFW operations.

Rules:

- no shell invocation,
- no concatenated command line strings,
- use `ProcessStartInfo.ArgumentList`,
- whitelist all command shapes,
- validate values before command construction,
- serialize mutations through a single writer lock,
- re-read firewall state after mutation.

## 7. Write Protocol Overview

The proposed write protocol has three cryptographic stages:

1. **Draft preflight signature**: admin signs `preflight envelope || hash(canonical draft)` to prove the draft is not random unauthenticated garbage.
2. **Daemon-signed change request**: daemon validates and canonicalizes the draft, stores a pending intent, and signs the preview.
3. **Admin-signed apply approval**: admin signs the daemon-issued canonical intent hash. Only this final signature authorizes mutation.

The first signature is an anti-spam/resource-abuse gate. The second signature authenticates what the daemon intends to apply. The third signature authorizes the actual firewall write.

## 8. Detailed Write Flow

### 8.1 Draft creation

The network admin creates, modifies, or deletes a rule in the Blazor UI.

The browser constructs canonical draft bytes. This draft is still untrusted input to the daemon, but the hash must cover the exact bytes submitted for preflight.

### 8.2 Preflight signature

The browser signs a preflight envelope:

```json
{
  "schema": "ufw-manager.preflight.v1",
  "issuedAt": "2026-05-14T12:00:00Z",
  "expiresAt": "2026-05-14T12:02:00Z",
  "clientNonce": "base64url-random-128-bit",
  "adminKeyId": "admin-frederik-laptop-p256-001",
  "operation": "PreflightDraft",
  "draftHash": "sha256(canonicalDraftBytes)"
}
```

The backend forwards:

```json
{
  "draft": "canonicalDraftBytes",
  "preflightEnvelope": { ... },
  "preflightSignature": "..."
}
```

### 8.3 Daemon preflight admission checks

Before expensive parsing or storing pending state, the daemon checks:

1. request size,
2. transport authentication,
3. request timeout,
4. basic framing sanity,
5. recomputed draft hash,
6. timestamp freshness,
7. client nonce replay cache,
8. admin key existence,
9. preflight signature validity,
10. admin key scope for preflight.

Only after these checks does the daemon parse deeply and canonicalize.

### 8.4 Daemon canonicalization and validation

The daemon converts the draft into a canonical semantic firewall change.

Validation includes:

- known operation type,
- known enum values,
- valid IP addresses and CIDRs,
- valid ports and port ranges,
- valid protocol/port combinations,
- local interface existence,
- valid direction/interface combinations,
- target rule existence for edit/delete,
- expected firewall generation,
- daemon policy checks,
- anti-lockout checks.

The daemon must not trust ASP.NET-side normalization.

### 8.5 Pending intent creation

If validation succeeds, the daemon creates and stores a pending intent:

- intent id,
- daemon nonce,
- expiry,
- requesting user id if forwarded and bound,
- preflight admin key id,
- canonical semantic change,
- command execution plan,
- firewall generation,
- firewall state hash,
- risk flags,
- human summary,
- UFW command preview.

The pending intent is the source of truth for execution. The client must not return an executable command string as authority.

For a first implementation, pending intents may be in-memory and expire quickly. A daemon restart invalidates pending approvals. For stronger auditability, persist them under:

```text
/var/lib/ufw-manager/pending/
```

### 8.6 Daemon-signed change request preview

The daemon returns a signed canonical preview:

```json
{
  "schema": "ufw-manager.change-request.v1",
  "intentId": "...",
  "daemonNonce": "...",
  "issuedAt": "2026-05-14T12:00:03Z",
  "expiresAt": "2026-05-14T12:05:03Z",
  "requestedByUserId": "...",
  "preflightAdminKeyId": "admin-frederik-laptop-p256-001",
  "operation": "CreateRule",
  "canonicalRule": {
    "action": "allow",
    "direction": "in",
    "interface": "dmz0",
    "protocol": "tcp",
    "source": "10.20.30.0/24",
    "destination": "10.100.200.10",
    "destinationPort": 443,
    "ipVersion": "ipv4"
  },
  "renderedPreview": {
    "humanSummary": "Allow inbound TCP 443 from 10.20.30.0/24 to 10.100.200.10 on dmz0",
    "ufwCommand": [
      "/usr/sbin/ufw",
      "allow",
      "in",
      "on",
      "dmz0",
      "proto",
      "tcp",
      "from",
      "10.20.30.0/24",
      "to",
      "10.100.200.10",
      "port",
      "443"
    ]
  },
  "firewallGeneration": 123,
  "firewallStateHash": "...",
  "riskFlags": []
}
```

The UFW command is a preview for review, not the executable authority.

### 8.7 Browser verifies daemon signature

The browser verifies the daemon signature before presenting the preview as trusted.

The daemon public key must be pinned or enrolled through a trusted setup flow. It must not be blindly fetched from the ASP.NET backend on each page load, because a compromised backend could provide a fake daemon key.

Acceptable first options:

- pin daemon public key fingerprint in frontend configuration,
- enroll daemon key during first setup with explicit fingerprint confirmation,
- store trusted daemon key locally after setup,
- later replace with a local native or hardware-backed signer.

### 8.8 Admin final approval signature

After review, the browser signs an apply approval envelope:

```json
{
  "schema": "ufw-manager.apply-approval.v1",
  "intentId": "...",
  "daemonNonce": "...",
  "daemonIntentHash": "sha256(canonicalChangeRequestBytes)",
  "adminKeyId": "admin-frederik-laptop-p256-001",
  "approvedAt": "2026-05-14T12:00:30Z"
}
```

This approval is forwarded through ASP.NET to the daemon.

### 8.9 Daemon apply checks

The daemon verifies:

1. pending intent exists,
2. intent has not expired,
3. daemon nonce matches,
4. intent is unused,
5. admin signature is valid,
6. admin key is active and not revoked,
7. admin key is authorized for the operation,
8. approval hash matches the locally stored canonical change request,
9. firewall generation/state hash is still acceptable,
10. rule target still exists for edit/delete,
11. interfaces still exist,
12. anti-lockout policy still passes,
13. quorum requirements are satisfied, if configured.

Only then does the daemon apply the change.

### 8.10 Execution

The daemon reconstructs the UFW command from its own cached canonical intent and execution plan.

Execution requirements:

- acquire mutation lock,
- snapshot current UFW state,
- optionally back up `/etc/ufw/user.rules` and `/etc/ufw/user6.rules`,
- execute with `ProcessStartInfo.ArgumentList`,
- capture stdout/stderr/exit code/duration,
- re-read UFW state,
- compute new firewall generation/hash,
- write audit result,
- return signed result.

The daemon must never execute a raw CLI command returned by the client or backend.

## 9. Read Operations

Read operations do not need admin signatures by default.

They should still require:

- ASP.NET authentication,
- ASP.NET authorization,
- IPC client authentication,
- rate limits,
- daemon-side route authorization if user identity is forwarded,
- audit logging for sensitive reads where useful.

The daemon should be the source of truth for live firewall state.

## 10. Canonicalization

Signatures require deterministic bytes.

The system should define one canonical representation for signed payloads, for example:

- RFC 8785-style canonical JSON,
- deterministic CBOR,
- custom versioned binary encoding.

Canonical JSON is easiest to debug. Whatever format is chosen, the rules must be strict:

- include `schema` in every signed object,
- include operation type,
- include expiry,
- include nonce,
- include key id,
- include hashes of referenced payloads,
- normalize IP addresses and CIDRs,
- normalize ports and port ranges,
- reject unknown security-critical fields unless extension handling is explicitly versioned.

## 11. Key Management

### 11.1 Admin keys

Admin private keys live on admin devices.

First implementation may use browser-held Web Crypto keys with non-extractable private keys. This is practical but not perfect: malicious same-origin code can still request signatures while the key is available.

Future stronger options:

- native local signing helper,
- hardware-backed signer,
- smartcard/YubiKey-backed signing,
- mobile approval device,
- quorum approval for high-risk operations.

### 11.2 Admin public key registry

The daemon owns the public key registry, for example:

```text
/etc/ufw-manager/keys.d/
```

Example key metadata:

```json
{
  "keyId": "admin-frederik-laptop-p256-001",
  "adminId": "frederik",
  "algorithm": "ECDSA-P256-SHA256",
  "publicKey": "...",
  "createdAt": "...",
  "expiresAt": "...",
  "revokedAt": null,
  "scopes": [
    "preflight:*",
    "apply:rule:create",
    "apply:rule:update",
    "apply:rule:delete"
  ]
}
```

Key enrollment should require one of:

- local root CLI on the firewall host,
- existing trusted admin signature,
- quorum of existing admins,
- physical console setup.

The ASP.NET backend must not be able to enroll privileged keys by itself.

### 11.3 Daemon signing key

The daemon signs previews and results.

Suggested private key path:

```text
/etc/ufw-manager/daemon-signing-key.pem
```

The browser must know the daemon public key through a pinned or trusted enrollment mechanism.

## 12. Authorization Model

There are two authorization layers.

### 12.1 Web authorization

ASP.NET decides whether a logged-in user may access UI/API routes:

- view firewall state,
- draft rules,
- request preflight,
- forward apply approvals,
- view audit data.

This is useful but not sufficient for firewall writes.

### 12.2 Daemon authorization

The daemon decides whether an admin key may approve a specific operation:

- key may preflight only,
- key may create rules but not delete rules,
- key may manage DMZ rules but not management-interface rules,
- key may require quorum for broad CIDRs,
- key may not change default policies,
- key may not affect management access without break-glass mode.

Daemon authorization is mandatory for writes.

## 13. Rule Model

The daemon should use a strongly typed semantic rule model.

Example shape:

```csharp
public sealed record FirewallRuleIntent(
    RuleOperation Operation,
    FirewallAction Action,
    TrafficDirection Direction,
    IpVersion IpVersion,
    NetworkInterfaceName? Interface,
    Protocol Protocol,
    CidrOrAny Source,
    CidrOrAny Destination,
    PortOrRange? SourcePort,
    PortOrRange? DestinationPort,
    RuleIdentity? TargetRule
);
```

The web app may have similar DTOs, but daemon-side parsing and validation are authoritative.

## 14. UFW Command Construction

The daemon builds command arguments from the canonical semantic model.

```csharp
ProcessStartInfo psi = new()
{
    FileName = settings.UfwPath,
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true
};

foreach (string argument in commandPlan.Arguments)
{
    psi.ArgumentList.Add(argument);
}
```

Never use shell concatenation:

```csharp
// Do not do this.
"ufw " + userInput
```

The command plan should be an argument array. The UI may render it as shell-like text for readability, but the daemon should never treat client-returned shell text as executable authority.

## 15. Anti-Lockout Guardrails

Because this runs on a central firewall, anti-lockout checks are mandatory.

The daemon should reject or escalate changes that would:

- remove the current management client access path,
- block the management VLAN/interface,
- disable SSH/admin access without an alternative path,
- change default policies,
- flush all rules,
- reload UFW into a state that blocks management,
- delete the last allow rule for management access.

For high-risk operations, require one of:

- local-console break-glass mode,
- two-admin quorum,
- rollback timer,
- staged apply with explicit confirmation.

A useful future pattern is confirmed apply:

1. Apply risky change with automatic rollback scheduled.
2. Admin confirms connectivity within a short window.
3. Daemon cancels rollback.
4. If no confirmation arrives, daemon restores previous rules.

## 16. Audit Logging

Every daemon decision should produce an audit event.

Audit events should include:

- event id,
- previous audit hash,
- timestamp,
- operation phase,
- request source identity,
- ASP.NET user id if forwarded,
- admin key id,
- daemon intent id,
- canonical intent hash,
- signature verification result,
- policy decision,
- command plan hash,
- before/after firewall generation,
- before/after firewall state hash,
- result.

Suggested storage:

```text
/var/log/ufw-manager/audit.log
/var/lib/ufw-manager/audit.sqlite
```

The web app may display audit data, but the daemon owns the audit source of truth.

## 17. Daemon Configuration

Suggested config path:

```text
/etc/ufw-manager/settings.json
```

Example:

```json
{
  "debugMode": false,
  "ufwPath": "/usr/sbin/ufw",
  "ipc": {
    "socketPath": "/run/ufw-manager/api.sock",
    "requestTimeout": "00:00:30",
    "maxConnections": 8,
    "maxMessageBytes": 65536,
    "requireMtls": true
  },
  "security": {
    "adminKeysPath": "/etc/ufw-manager/keys.d",
    "daemonSigningKeyPath": "/etc/ufw-manager/daemon-signing-key.pem",
    "preflightMaxAgeSeconds": 120,
    "intentMaxAgeSeconds": 300
  },
  "audit": {
    "path": "/var/log/ufw-manager/audit.log",
    "hashChain": true
  },
  "policy": {
    "protectManagementAccess": true,
    "managementInterfaces": ["mgmt0"],
    "managementCidrs": ["10.100.100.0/24"]
  }
}
```

Config files should be root-owned with strict permissions.

## 18. systemd Hardening

Initial unit sketch:

```ini
[Unit]
Description=UFW Manager Daemon
After=network.target

[Service]
Type=simple
ExecStart=/usr/local/lib/ufw-manager/ufw-managerd serve --config /etc/ufw-manager/settings.json
Restart=on-failure
RestartSec=2s

User=root
Group=root

NoNewPrivileges=yes
PrivateTmp=yes
ProtectHome=yes
ProtectSystem=strict
ReadWritePaths=/etc/ufw /etc/ufw-manager /run/ufw-manager /var/lib/ufw-manager /var/log/ufw-manager

CapabilityBoundingSet=CAP_NET_ADMIN CAP_NET_RAW
AmbientCapabilities=CAP_NET_ADMIN CAP_NET_RAW
RestrictAddressFamilies=AF_UNIX AF_INET AF_INET6 AF_NETLINK
SystemCallFilter=@system-service
LockPersonality=yes
MemoryDenyWriteExecute=yes
RestrictRealtime=yes
RestrictSUIDSGID=yes

[Install]
WantedBy=multi-user.target
```

The exact capability set must be verified with the real UFW execution path. If UFW requires broader permissions, document the reason explicitly.

## 19. Web Container Hardening

The ASP.NET container should run as non-root and should not have host privileges.

Recommended constraints:

- no Docker socket mount,
- no privileged mode,
- no host PID namespace,
- no host network namespace unless explicitly justified,
- read-only root filesystem where practical,
- writable volume only for app database and temp data,
- IPC socket mounted only at the required path,
- no `/etc/ufw` mount,
- no `/usr/sbin/ufw` mount,
- drop Linux capabilities,
- resource limits,
- health checks.

## 20. Error Handling

The daemon should return structured errors with stable codes:

```text
ERR_INVALID_SIGNATURE
ERR_STALE_PREFLIGHT
ERR_REPLAYED_NONCE
ERR_UNKNOWN_ADMIN_KEY
ERR_REVOKED_ADMIN_KEY
ERR_UNAUTHORIZED_KEY_SCOPE
ERR_INVALID_RULE_MODEL
ERR_INTERFACE_NOT_FOUND
ERR_FIREWALL_STATE_CHANGED
ERR_LOCKOUT_RISK
ERR_INTENT_EXPIRED
ERR_INTENT_ALREADY_USED
ERR_UFW_EXECUTION_FAILED
```

Production error messages should be useful but should not leak unnecessary internal details. Detailed diagnostics belong in daemon audit logs.

## 21. Testing Strategy

Unit tests:

- canonicalization stability,
- signature verification,
- replay cache behavior,
- rule validation,
- command plan generation,
- policy decisions,
- anti-lockout checks,
- error mapping.

Property/fuzz tests:

- malformed drafts,
- invalid enum values,
- Unicode/interface-name edge cases,
- IPv4/IPv6 normalization,
- CIDR edge cases,
- port range edge cases,
- canonical JSON equivalence.

Integration/security regression tests:

- ASP.NET tries to apply without admin signature,
- ASP.NET modifies draft after preflight signature,
- ASP.NET modifies daemon preview before browser approval,
- old approval is replayed,
- approval is forwarded for the wrong intent,
- key is revoked between preview and apply,
- firewall state changes between preview and apply,
- daemon restart invalidates pending intents.

## 22. Implementation Milestones

### Milestone 1: Secure local IPC baseline

- Replace hardcoded loopback TCP with Unix domain socket.
- Remove `NoTransportSecurityService` from production path.
- Add message size limits, timeouts, and rate limits.
- Add daemon config validation and strict file permissions.
- Add basic daemon audit log.

### Milestone 2: Canonical rule model and command planner

- Define semantic rule/change model.
- Implement daemon-side normalization and validation.
- Implement UFW command argument planner.
- Add tests for command generation.
- Add dry-run/preview rendering.

### Milestone 3: Preflight signature gate

- Add admin key registry.
- Add preflight signed envelope.
- Add draft hashing and nonce replay cache.
- Add daemon-side preflight admission checks.

### Milestone 4: Daemon-signed intent preview

- Add pending intent store.
- Add daemon signing key.
- Add canonical change request format.
- Add browser-side daemon signature verification.
- Add UI review screen.

### Milestone 5: Signed apply flow

- Add admin apply signature.
- Add apply endpoint.
- Add state generation/hash checks.
- Add single-writer mutation lock.
- Add UFW execution.
- Add signed apply result.

### Milestone 6: Hardening and deployment

- systemd hardening,
- container hardening,
- backup/rollback handling,
- anti-lockout enforcement,
- audit display,
- operational documentation.

### Milestone 7: Blazor WebAssembly migration

- Split current Razor/Web app into API and Blazor client if desired.
- Keep privileged authorization in daemon.
- Add key enrollment UX.
- Add signature prompts and trusted daemon fingerprint UX.

## 23. Open Questions

1. Should the first implementation use browser-held Web Crypto keys, or immediately use a local native/hardware-backed signer?
2. Should risky operations require quorum from the start?
3. Should pending intents survive daemon restart?
4. Should the daemon only invoke UFW CLI, or eventually manage lower-level rules/config directly?
5. How should UFW numbered rules be modeled, given that numbering shifts after insert/delete operations?
6. Should rollback timers be required for all changes touching management access?
7. Should ASP.NET keep a desired-state database, or should daemon-read firewall state be the only source of truth?
8. How should IPv6 be represented in the initial UI?
9. Is this only for one local firewall, or should the protocol anticipate multiple firewall nodes later?

## 24. Summary

The revived architecture should keep the existing privilege-separated direction but harden the boundary substantially.

Important design decisions:

- The daemon is the policy authority.
- The web backend is not trusted for writes.
- ASP.NET Identity is necessary but not sufficient for mutation.
- mTLS/socket authentication protects the daemon API, but does not authorize writes.
- Browser preflight signatures prevent unauthenticated draft spam.
- Daemon-signed previews let the browser verify what the daemon intends to apply.
- Final admin signatures authorize only daemon-issued canonical intents.
- The daemon executes only its own cached canonical intent.
- Command execution uses strict argument arrays, never shell concatenation.
- Anti-lockout checks and audit logging are core requirements.

The complexity is intentional and concentrated at the only place where it matters: the boundary between an unprivileged management UI and a privileged firewall mutation service.
