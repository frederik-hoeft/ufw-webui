# UFW WebUI

UFW WebUI is a browser-based management interface for UFW that keeps privileged firewall execution outside the web application. The browser and ASP.NET application handle presentation, authentication, and management workflows; a small host daemon is the only component allowed to execute UFW commands.

The design is intended for a Linux host where UFW remains the firewall authority and where the web tier should not become a privileged firewall process. Rules created outside UFW WebUI remain visible, and supported rules can be addressed by their semantics rather than by unstable `ufw status numbered` positions.

## What it provides

The management surface includes:

- authoritative UFW rule listing;
- browser-signed add and delete operations;
- host network-interface discovery with application-owned comments and visibility metadata;
- ASP.NET Core Identity authentication with short-lived access tokens and rotating refresh tokens;
- a platform-neutral UFW mock for development on systems without UFW, including Windows;
- a local typed IPC protocol between the web application and privileged daemon.

Rule reordering and ordered insertion have UI groundwork but are not part of the signed backend mutation contract.

## Security model in brief

A normal authenticated web session is not sufficient authority to modify the firewall. Mutations carry a separate ECDSA P-256 signature created by the administrator's browser and verified independently by `Ufw.Systemd` against daemon-managed authorized public keys. The signature binds the exact operation, normalized rule semantics, daemon deployment identity, timestamp, and nonce. The daemon also persists replay state before starting a privileged mutation.

Production deployment separates frontend delivery from ASP.NET. A non-root nginx container owns the immutable browser assets and proxies `/api/*` to a private `Ufw.Web` container. This matters because browser code handles mutation-signing keys: compromising ASP must not give an attacker a direct way to replace the signing client with key-capture code.

UFW remains authoritative. PostgreSQL stores users, refresh-token state, and application metadata, but it is not a shadow firewall database.

See [Security architecture](docs/architecture/security.md) for the complete trust model.

## Production topology

A supported production deployment consists of:

- a privileged `Ufw.Systemd` service on the firewall host;
- a public non-root nginx container serving `Ufw.Client` and terminating browser TLS;
- a private non-root `Ufw.Web` container reachable from nginx only through a Unix-domain socket;
- PostgreSQL on an internal container network reachable only by `Ufw.Web`.

Rootful and rootless Docker use the same application topology but require different host ownership and group mappings. Start with the [deployment guide](docs/deployment/deployment.md) and choose the runbook for the Docker mode actually used on the host.

## Local development

The solution targets .NET 10. A normal source build is:

```bash
dotnet restore src/Ufw.slnx
dotnet build src/Ufw.slnx --no-restore
dotnet test src/Ufw.slnx --no-restore --no-build
```

Local development also needs PostgreSQL plus matching development credentials/configuration for the web application and daemon. The repository provides both:

```bash
docker compose up -d postgres
./scripts/setup-dev.sh
```

On Windows, build `Ufw.Mock` first and point the generated daemon configuration at the mock instead of native UFW. See [Local development](docs/development/local-development.md) for the complete setup and run sequence.

## Documentation

Start with the document that matches what you are trying to understand:

- [Architecture overview](docs/architecture/architecture-overview.md) explains the system model, component boundaries, state ownership, and major request flows.
- [Firewall model](docs/architecture/firewall-model.md) explains authoritative UFW state, normalization, semantic rule identity, and mutation reconciliation.
- [Security architecture](docs/architecture/security.md) explains trust boundaries, browser signing, replay protection, web authentication, and privileged execution.
- [IPC protocols](docs/protocols/README.md) describes the versioned wire, application-envelope, and signed-intent contracts.
- [Deployment](docs/deployment/deployment.md) selects between the rootful and rootless production runbooks and links operational/configuration reference material.
- [IPC test adapter](docs/testing/ipc-test-adapter.md) documents the production-equivalent in-process test harness.
- [UFW mock](docs/development/ufw-mock.md) documents the development substitute used when native UFW is unavailable.

`docs/internal` is reserved for temporary maintainer notes and open work. It is not part of the steady-state project documentation.

## Source layout

The production code is split by trust and deployment boundary rather than by one monolithic application:

| Project | Role |
| --- | --- |
| `Ufw.Client` | Blazor WebAssembly browser application |
| `Ufw.Web` | ASP.NET Core REST API, authentication, PostgreSQL-backed application state, daemon IPC client |
| `Ufw.Systemd` | privileged host daemon and UFW execution boundary |
| `Ufw.Shared` | cross-process firewall semantics, security primitives, and IPC contracts |
| `Ufw.Ipc.Client` | typed client for the daemon IPC protocol |
| `Ufw.Roslyn` / `Ufw.Roslyn.SourceGen` | daemon routing and serialization source-generation support |
| `Ufw.Mock` | development-only UFW-compatible command substitute |

Test projects live beside the production projects in `src/` and exercise shared semantics, IPC, daemon behavior, ASP services, and the mock CLI.

## Contributing

Follow [`code-style.md`](code-style.md) for source conventions. Architectural changes should preserve the core ownership boundaries described in the architecture documentation: the web tier does not become firewall authority, privileged mutations remain independently authorized by the daemon, and unsupported UFW state remains visible rather than being guessed into a mutable model.

Before submitting changes, run the full solution build and test suite. Changes to IPC or mutation authorization should also update the corresponding protocol document when the wire contract changes.

## License

UFW WebUI is licensed under the [MIT License](LICENSE).
