# UFWeb coding instructions

## Project model

UFWeb is a .NET 10 UFW management platform with a Blazor WebAssembly client, a network-facing ASP.NET Core API, and a privileged host daemon.

- `Ufw.Web.Client` is the MudBlazor-based browser frontend. It owns presentation, in-memory HTTP authentication state, rule-authoring interaction, and browser-side signed-intent creation.
- `Ufw.Web` is the REST API. It owns ASP.NET Core Identity, PostgreSQL-backed application state, JWT/refresh-token handling, application authorization, browser-facing metadata, and the local
  IPC client.
- `Ufw.Systemd` is the privileged daemon and the authority for actual UFW observation and execution.
- `Ufw.Shared` owns cross-process firewall semantics, security primitives, and the `Ufw.Shared.Ipc` protocol/serialization contract.
- `Ufw.Ipc.Client` implements the typed local IPC client on top of `Ufw.Shared.Ipc`.
- `Ufw.Roslyn` and `Ufw.Roslyn.SourceGen` provide the runtime/source-generator boundary for daemon routing and serialization.

Read [the architecture overview](../docs/architecture/architecture-overview.md) before changing subsystem boundaries and [the security architecture](../docs/architecture/security.md) before
working on authentication, authorization, IPC security, or firewall mutations.

## Engineering conventions

Treat [`code-style.md`](../code-style.md) and the repository `.editorconfig` as authoritative for C# style. Apply them to new code and keep touched code consistent rather than introducing a
second local convention. In particular, prefer explicit types over `var`, file-scoped namespaces, one top-level type per file, and the repository naming/modifier conventions.

Do not wrap code mechanically at 80 or 120 columns. Keep ordinary declarations, calls, and parameter lists on one line while they remain readable; use roughly 190-200 characters as the point
where length alone justifies wrapping. Wrap earlier only when the structure is materially clearer that way, such as one logical item per line in a long initializer or fluent expression.

Keep types specialized. A service or component should own one coherent responsibility and expose the smallest useful boundary for it. When a component, controller, or service starts
combining independent orchestration, validation, projection, persistence, navigation, or host-integration concerns, extract those concerns behind focused DI services instead of growing a
monolith or introducing a page-sized manager/facade. Prefer interfaces at boundaries that benefit from substitution or isolated tests; do not add interfaces mechanically to value objects or
self-contained implementation details. Do not split a cohesive parser, algorithm, concurrency primitive, protocol implementation, or state machine merely because the file is long.

Treat static helpers as a design smell when they encode application policy, depend on replaceable collaborators, or make behavior difficult to test in isolation. Move those responsibilities
into injected services. Pure algorithms, constants, extension methods, and other genuinely stateless language-level utilities may remain static when DI would add no useful boundary.

Group namespaces by stable subsystem/domain boundaries rather than mirroring every directory. Reorganizing a namespace is appropriate when it makes ownership clearer, but avoid subnamespaces
that only add ceremony.

Treat generated output as generated output. Do not hand-edit EF migration designer files, compiled Sass output, or source-generator artifacts for style cleanup; fix the source/template when
generated output is actually wrong.

For URLs and query strings, use the shared `Ufw.Shared.Web` URI utilities instead of composing routes through string interpolation or manual delimiter/escaping logic.

For component-specific Sass, prefer nesting under the component's meaningful root class or semantic element instead of adding one-off presentation classes to the DOM. Introduce a class when
it represents a reusable state, role, or styling hook, not merely to address one descendant once.

User-facing copy and permanent documentation describe the steady-state product. Avoid changelog phrasing such as "now supports", "was changed to", or "new behavior" unless history itself is
relevant to the user or architectural constraint.

## Security constraints

Treat `Ufw.Web` as untrusted relative to the daemon for privileged mutation authority. HTTP authentication, IPC reachability, and optional mTLS peer authentication are not sufficient proof
for a firewall change.

Mutating daemon endpoints must use the deployment-scoped signed-intent boundary. `Ufw.Systemd` verifies an authorized ECDSA P-256 signature, deployment identity, freshness, durable replay
state, and the complete operation-specific mutation semantics before UFW execution. Append, ordered insertion, delete, and reorder all use this mechanism; reuse the shared intent protocol
rather than creating endpoint-specific authorization conventions. See [signed mutation intent v2](../docs/protocols/signed-intent.md).

The daemon-facing transport is local named-pipe/Unix-domain IPC. Do not add a network transport for the privileged daemon.

UFW/daemon state is authoritative. PostgreSQL may store users, refresh-token state, and application-owned authoring metadata, but it must not become a second source of truth for firewall
rules or host state.

## Web API and client conventions

Use controller-based, versioned browser APIs under `Ufw.Web/Api/V{N}`. Keep request/response contracts near the versioned API surface and place reusable application logic behind focused
services.

Authentication infrastructure consists of:

- ASP.NET Core Identity with EF Core/PostgreSQL;
- ES256 access tokens signed with a P-256 application key;
- opaque rotating refresh tokens stored as hashes in PostgreSQL and delivered through a `Secure`, `HttpOnly`, `SameSite=Strict` cookie;
- CORS only where the development client and API run on separate origins. Production is same-origin behind nginx.

`Ufw.Web` configuration has one optional local JSON source: the gitignored `src/Ufw.Web/appsettings.json`. Keep `src/Ufw.Web/appsettings.default.json` as the committed template/reference,
and use environment variables or command-line overrides for deployment. Do not reintroduce environment-specific appsettings files or user-secrets configuration.

Bootstrap users are initial provisioning only. Keep `Auth:Bootstrap:Users` idempotent and non-destructive: create missing accounts through ASP.NET Core Identity, never reset an existing
password from configuration, and never delete users merely because they disappear from bootstrap configuration. Standard ASP.NET Core configuration providers, including Docker environment
variables, must remain sufficient to drive bootstrap.

Keep browser code in `Ufw.Web.Client` with explicit ownership boundaries: Razor presentation lives under `UI`, ASP REST clients/contracts live under `Api` with controller-shaped resource
namespaces and `Model` DTO subnamespaces, client domain/application behavior lives under `Features/<domain>`, immutable runtime configuration lives under `Configuration`, and only truly
domain-agnostic browser/application services live under `Services`. Do not add a generic `Infrastructure` dumping ground. Keep page/component SCSS beside its Razor owner under `UI`; use
`.razor.scss` for natural CSS isolation and ordinary `.scss` when MudBlazor/portal/render-fragment styling would otherwise require pervasive `::deep`; register isolated Sass companions
explicitly in `sasscompiler.json` so isolation remains a per-component choice. `UI/Styles/app.scss`, generated `wwwroot/css/app.css`, and the generated isolated stylesheet bundle are build
concerns and generated CSS must not be edited or committed. Do not reintroduce Razor Pages or UI assets into `Ufw.Web`, and do not move privileged host behavior into the browser.

Access tokens stay in memory; refresh-token cookies remain `HttpOnly`. Client operations that mutate the rotating refresh-token cookie must use the shared authentication-operation
coordinator so concurrent tabs cannot consume the same token family member. Production uses one HTTPS origin for browser and API traffic; development may use the configured CORS origin
because the processes run separately.

Mutation private keys must not be persisted by the client unless a later design explicitly introduces a secure key-storage boundary. Persisting non-sensitive UI preferences such as
appearance or culture is allowed, but browser storage must not become a general-purpose authentication or secret store.

Keep expected browser/API failures behind the client error-classification boundary. Do not render arbitrary exception messages in Razor components. Distinguish an absent/expired
authentication session from API unavailability or an incompatible response, and preserve explicit startup/freshness failure states instead of converting infrastructure failures into
anonymous authentication state.

Use injected `TimeProvider` for client-side token-expiry and signed-intent timestamps rather than reading the wall clock directly. Treat daemon-backed rule state as a freshness-tracked
authoritative snapshot. Do not infer firewall status or an empty rule set before the first successful read, and do not enable mutations against stale state. If refresh or mutation
reconciliation fails, preserve the last confirmed snapshot only as explicitly stale state until a fresh daemon read succeeds.

Keep the browser UI usable with keyboard, assistive technology, and narrow viewports. Routed pages must expose a single semantic `h1`, authenticated layouts must preserve explicit
navigation/main landmarks, icon-only actions require accessible labels, and responsive rule-table cells must retain meaningful `DataLabel` text. Use modal dialogs for destructive
confirmations so focus stays with the active interaction instead of rendering confirmation UI away from the initiating row.

## Daemon conventions

Daemon IPC controllers use the existing route attributes and source-generated endpoint map. Keep privileged UFW execution, host inventory, authoritative rule parsing, signed-intent
verification, and mutation reconciliation inside the daemon rather than moving host-facing behavior into `Ufw.Web`.

All daemon-managed UFW reads and mutations share the execution gate. Keep state-conditioned operations conservative: exact-snapshot insertion/reorder must fail rather than reinterpret stale
occurrence coordinates, and any reorder delete/reinsert obligation must be recovered or left as a durable fail-closed condition before later mutations proceed.

Permanent documentation should describe steady-state architecture and behavior; temporary implementation sequencing belongs only under `docs/internal` while it remains active.
