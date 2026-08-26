# UFW WebUI Architecture and Assets

This document provides a concise, security-focused architectural baseline of the UFW WebUI system to support threat modeling, risk assessment, and future security control design. It intentionally omits low-level implementation details and concentrates on components, trust boundaries, data flows, assets, and existing / planned security controls.

## 1. High-Level System Overview

UFW WebUI enables secure remote management of Linux firewall (UFW) rules through a web interface. The design applies strict privilege separation: an unprivileged, dockerized web application handles UI, identity, and business logic; a separate privileged systemd service on the host performs firewall operations. Communication between the two uses structured, authenticated, serialized messages over named pipes with HTTP-style semantics. The privileged service executes UFW commands and uses grammar-based parsers to convert command outputs into strongly typed models.

```
┌─────────────────┐  CRUD operations ┌───────────────────┐ UFW Command model┌───────────────────┐  UFW Commands  ┌─────────┐
│   Web Browser   │ ◄─────────────►  │  Ufw.Web          │ ◄─────────────►  │ systemd service   │ ◄────────────► │   UFW   │
│   (User)        │      HTTPS       │ (dockerized/user) │    IPC Channel   │ (host/root)       │   subprocess   │ Daemon  │
└─────────────────┘                  └───────────────────┘                  └───────────────────┘                └─────────┘
                                             │                                                                        │
                                             ▼                                                                        ▼
                                      ┌──────────────┐                                                       ┌─────────────────┐
                                      │   SQLite     │                                                       │ System Firewall │
                                      │  Database    │                                                       │    Rules        │
                                      └──────────────┘                                                       └─────────────────┘
```

## 2. Core Components

| Component | Role | Privilege Level | Notes |
|-----------|------|-----------------|-------|
| Browser (User) | Initiates management actions | External | Authenticated via Web App UI |
| Ufw.Web | Presents UI, enforces authZ, normalizes input, persists metadata | Unprivileged (container/user) | ASP.NET Core Razor Pages + EF Core + Identity |
| SQLite DB | Stores user accounts, session data, rule metadata | Unprivileged | Local file-based persistence |
| Named Pipe IPC Layer | Transports request/response messages | Boundary | Mutual TLS over stream |
| Ufw.Systemd Service | Validates, routes, executes firewall commands | Privileged / root | AOT compiled CLI program |
| UFW CLI / iptables | Enforces firewall rules | Kernel / root | Access via controlled command set |

## 3. Trust Boundaries

1. External User to Web Application (HTTPS termination / session boundary)
2. Web Application process to Named Pipe endpoint (local IPC boundary)
3. Named Pipe endpoint to Privileged Service (privilege elevation boundary)
4. Privileged Service to UFW CLI / underlying OS firewall (system command boundary)
5. Persistence boundary (SQLite file permissions, potential leakage vectors)

## 4. Data & Asset Inventory

Primary Assets (Confidentiality / Integrity / Availability considerations):
* Firewall Rule State (authoritative firewall configuration executed by UFW)
* Rule Metadata (UI-level abstractions, stored in SQLite, rule templates, deactivated rules, etc.)
* User Credentials & Sessions (Identity tables, password hashes, tokens)
* IPC Message Stream (Request/Response envelopes + payloads, mutual TLS)
* Configuration Files (`/etc/ufw-manager/settings.json`, appsettings.json)
* Certificate / Key Material (mutual TLS on pipe)
* Internal Root CA (provisioning and signing of pipe certificates)
* Logs & Debug Traces (may contain operational or sensitive context)

Supporting Assets:
* Normalization / Validation Rules in Ufw.Web (input integrity)
* Grammars & Parsers (firewall command output interpretation)

## 5. Principal Data Flows

Flow A: Rule Listing
1. User requests rule list via browser (HTTPS).
2. Web app authenticates user; invokes service layer.
3. `IUfwClient` sends GET message over named pipe.
4. Privileged service routes request via generated mapping; executes UFW list command.
5. Output parsed to structured model; response serialized and returned.
6. Web app formats data; returns HTML/JSON to browser.

Flow B: Rule Creation / Modification
1. User submits rule form.
2. Web app validates + normalizes input (normalizer pipeline).
3. IPC POST request sent to privileged service.
4. Service validates payload; constructs & executes UFW command.
5. Result parsed; success or error returned.
6. Web app persists metadata, updates UI state.

Flow C: Configuration Reload
1. Operator triggers service reload (systemd or sysctl).
2. Privileged service reads root-owned config file.
3. New settings applied to workers / timeouts / pipe security.