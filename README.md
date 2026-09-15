# UFW WebUI

UFW WebUI is a browser-based management interface for UFW built around a strict privilege boundary: the network-facing application never executes firewall commands. A Blazor client and ASP.NET Core API provide the management experience, while a small host daemon is the only component allowed to invoke UFW.

UFW remains the firewall authority. Rules created with the normal UFW CLI or by other administrators remain visible, mutations use structural semantics or exact snapshot occurrences rather than treating unstable UFW row numbers as durable identity, and PostgreSQL stores application state rather than a shadow copy of the firewall.

## Overview

The management path stays split across those boundaries rather than turning the web tier into a privileged firewall process:

- **Authoritative firewall presentation** — Read firewall activity, IPv4/IPv6 rules, IPv6 capability, and default policies directly from UFW through the host daemon. Unsupported rule syntax remains visible but read-only.
- **Signed firewall mutations** — Append, ordered insertion, delete, and reorder operations are signed in the browser and independently authorized by the daemon before UFW execution.
- **State-conditioned ordering** — Ordered insertion and reordering bind placement to the exact authoritative snapshot reviewed by the administrator, so duplicate rules and ordinary UFW renumbering do not create ambiguous mutation targets.
- **Rule-authoring metadata** — Host network interfaces can carry application-owned comments and visibility preferences, while known-host aliases provide reusable literal addresses and networks without entering the firewall contract.
- **Web authentication** — ASP.NET Core Identity, short-lived ES256 access tokens, and rotating opaque refresh tokens protect the HTTP API independently from firewall mutation authorization.
- **Production-oriented separation** — nginx serves immutable frontend assets and browser TLS, `Ufw.Web` runs privately behind a Unix socket, and PostgreSQL is isolated on an internal container network.
- **Cross-platform development** — `Ufw.Mock` emulates the UFW command surface used by the daemon, allowing the production firewall path to be exercised without Linux firewall privileges, including on Windows.

## Security model

A valid web session is not sufficient authority to change the firewall. Every privileged mutation carries an ECDSA P-256 signature created by the administrator's browser and verified by `Ufw.Systemd` against daemon-managed authorized public keys. The signed intent binds the exact operation, its rule or ordering payload, the daemon deployment identity, issuance time, and a single-use nonce.

The daemon validates the signed semantics again, persists replay state before starting a mutation, renders validated argv, and executes UFW directly without a shell. `Ufw.Web` can forward a signed request but cannot manufacture mutation authority.

Frontend delivery is separated from ASP for the same reason. Browser code handles mutation-signing material, so production nginx serves an independently built, read-only frontend image rather than allowing a compromised ASP process to replace the signing client.

See [Security architecture](docs/architecture/security.md) for the complete trust model and [Signed mutation intent v2](docs/protocols/signed-intent.md) for the authorization contract.

## Production topology

A supported production deployment consists of:

- a privileged `Ufw.Systemd` service running directly on the firewall host;
- a public non-root nginx container serving `Ufw.Client` and terminating browser TLS;
- a private non-root `Ufw.Web` container reached by nginx through a Unix-domain socket;
- PostgreSQL on an internal container network reachable only by `Ufw.Web`.

Rootful and rootless Docker use the same application topology but different host ownership and group mappings. Start with the [deployment guide](docs/deployment/deployment.md) and use the runbook for the Docker mode actually used on the host.

## Local development

The solution targets .NET 10. A normal source build is:

```bash
dotnet restore src/Ufw.slnx
dotnet build src/Ufw.slnx --no-restore
dotnet test src/Ufw.slnx --no-restore --no-build
```

The development stack uses PostgreSQL plus generated credentials/configuration for the browser, ASP application, and daemon:

```bash
docker compose up -d postgres
./scripts/setup-dev.sh
```

On Windows, build `Ufw.Mock` first and let the generated daemon configuration use the mock instead of native UFW. See [Local development](docs/development/local-development.md) for the complete setup and run sequence.

## Documentation

| Document | Audience and purpose |
| --- | --- |
| [Architecture overview](docs/architecture/architecture-overview.md) | Contributors: system boundaries, state ownership, and primary request flows |
| [Firewall model](docs/architecture/firewall-model.md) | Contributors: authoritative UFW state, semantic rule identity, ordering, and mutation reconciliation |
| [Security architecture](docs/architecture/security.md) | Contributors/operators: trust boundaries, browser signing, replay protection, and privileged execution |
| [Compile-time routing and serialization](docs/architecture/source-generation.md) | Contributors: NativeAOT-oriented source-generation architecture |
| [IPC protocols](docs/protocols/README.md) | Protocol implementers/reviewers: transport, application envelopes, and signed intents |
| [Deployment](docs/deployment/deployment.md) | Operators: production topology and rootful/rootless runbook selection |
| [Local development](docs/development/local-development.md) | Contributors: development prerequisites, generated credentials, and run sequence |
| [UFW mock](docs/development/ufw-mock.md) | Contributors/test authors: platform-neutral UFW command emulation |
| [IPC test adapter](docs/testing/ipc-test-adapter.md) | Test authors: production-equivalent in-process IPC testing |

`docs/internal` contains temporary, non-normative maintainer notes for unresolved work. Implemented behavior belongs in the architecture, protocol, deployment, development, or testing documentation above.

## Project structure

The source tree follows trust and deployment boundaries:

| Project | Role |
| --- | --- |
| `Ufw.Client` | Blazor WebAssembly browser application |
| `Ufw.Web` | ASP.NET Core REST API, authentication, PostgreSQL-backed application state, and daemon IPC client |
| `Ufw.Systemd` | privileged host daemon and UFW execution boundary |
| `Ufw.Shared` | cross-process firewall semantics, security primitives, and IPC contracts |
| `Ufw.Ipc.Client` | typed client for the daemon IPC protocol |
| `Ufw.Roslyn` / `Ufw.Roslyn.SourceGen` | runtime contracts and source-generated daemon routing/serialization bindings |
| `Ufw.Mock` | development-only UFW-compatible command substitute |

Test projects live beside the production projects under `src/` and cover shared semantics, IPC, daemon behavior, ASP services, browser-side policy, and the mock CLI.

## Contributing

Follow [`code-style.md`](code-style.md) for source conventions. Before changing subsystem boundaries, read the [architecture overview](docs/architecture/architecture-overview.md); before changing authentication, authorization, IPC security, or firewall mutations, read the [security architecture](docs/architecture/security.md).

Before submitting changes, run the full solution build and test suite. Changes to IPC or mutation authorization should update the corresponding protocol document whenever the wire or signing contract changes.

## License

UFW WebUI is licensed under the [MIT License](LICENSE).
