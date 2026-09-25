# Browser Application Architecture

`Ufw.Web.Client` is a Blazor WebAssembly application, but its architectural boundary is not the Razor component tree. The client is split so that transport, application behavior, reusable browser services, and presentation can evolve independently while all firewall authority still comes from server/daemon snapshots.

The most important rule is that the browser may derive, stage, and present state, but it does not invent authoritative firewall state. A fresh rule inventory is the input to filtering, family presentation, ordering preview, metadata enrichment, and mutation signing; those browser-side projections never replace the snapshot they came from.

## Client layers

The project uses five top-level application areas:

```text
Ufw.Web.Client/
  Api/             HTTP clients and generic REST mechanics
  Configuration/   immutable browser runtime configuration
  Features/        client-side domain/application behavior
  Services/        domain-agnostic browser/application services
  UI/              Razor presentation and styles
```

The shared `Ufw.Web.Model` project sits outside the client and contains pure, versioned REST DTOs used by both ASP and Blazor.

### API

`Api` is the HTTP boundary. Resource subnamespaces mirror the browser-visible ASP resources (`Auth`, `Intent`, `KnownHosts`, `NetworkInterfaces`, `Rules`, `RuleMetadata`, `RuleTags`, and `Status`). Each resource owns its typed HTTP client interface and implementation; general response/error handling and the source-generated JSON context live at the API root.

API clients do not own feature policy. For example, a rules API client can issue an HTTP request, but deciding how to reconcile an uncertain mutation or how a reorder preview becomes a signed request belongs to the rules feature.

### Features

`Features` contains browser-side application behavior grouped by domain. Authentication, known hosts, network interfaces, status, and rules each own the state/services that give HTTP data application meaning.

The rules feature is the largest domain and is further split by responsibility: authoring, filtering, insertion, intent signing/mutation orchestration, metadata, ordering, presentation text, and page projections. These services are ordinary testable .NET services rather than Razor code whenever they do not require component lifecycle or DOM access.

### Services

`Services` is intentionally small and cross-domain. Clipboard access, local storage, localization, theming, and client error mapping live here because they are reusable browser/application capabilities rather than part of a firewall feature.

Feature-specific services should not be moved into this namespace merely because they use dependency injection.

### UI

`UI` contains `App`, pages, layouts, reusable Razor components, and styles. It depends on feature/API/service abstractions and translates their state into interaction and presentation.

The filtering UI is a useful boundary example: filter semantics and evaluation live in `Features.Rules.Filtering`, while the catalogue that maps filter types to editor components and human-facing labels lives in `UI.Components.Rules.Filtering`. This keeps the feature layer independent from Razor component types.

### Configuration

`Configuration` contains immutable browser runtime settings such as the resolved API base address. Browser configuration is public by definition and must not contain secrets.

## Rule inventory state

The rules and create-rule pages share an explicit inventory model for the currently loaded firewall snapshot. Its state distinguishes initial loading, refreshing, usable fresh state, stale state after a failed refresh or uncertain mutation, and terminal load failure.

That state machine exists because "we still have a snapshot" and "that snapshot is safe to mutate" are different questions. A failed refresh may leave useful information on screen, but mutation controls must remain disabled until freshness is restored.

A successful REST response is projected into a browser `RuleSnapshot` containing the daemon-authoritative firewall data plus matching ASP-owned metadata. Subsequent UI operations derive views from that snapshot instead of rewriting it.

The rules page separately tracks transient interaction modes such as metadata editing, deletion, and applying a reorder. Keeping workflow state separate from inventory freshness prevents combinations such as a busy dialog flag accidentally becoming evidence that firewall data is current.

## Family projection and ordering

UFW evaluates IPv4 and IPv6 in separate ordered rule sets. The browser therefore projects one authoritative combined snapshot into independent family workspaces. User-visible positions are one-based within the selected family; the combined UFW numbering remains protocol/snapshot data used for daemon-side addressing.

A drag/drop or move action creates a local ordering preview. It does not mutate the authoritative snapshot. Applying the preview uses the complete desired occurrence order plus the fingerprint of the reviewed authoritative baseline to create a signed reorder request. Discarding the preview simply drops the derived ordering state.

Ordered insertion follows a similar distinction. Navigation from a specific row carries snapshot-local insertion context into the create-rule workflow. The browser can present the chosen before/after position, but the eventual signed request remains conditioned on the exact authoritative snapshot that gave the anchor occurrence its meaning.

## Query and filtering pipeline

Rule filtering is a composable client-side pipeline over the current family projection. `RuleQuery` is a collection of independent `RuleFilter` instances rather than one monolithic search predicate, which allows filters to be combined, added, removed, and edited without coupling unrelated semantics.

The current filter set covers:

- action;
- direction;
- protocol;
- source/destination address or network;
- source/destination/any port expressions;
- interface;
- reusable tag identity;
- free-text search.

Network matching is CIDR-aware, and filter editors use the selected address family so IPv4/IPv6 validation stays explicit. Known-host aliases can contribute human-facing context to network fields without becoming part of the canonical rule.

Free-text search runs over a presentation/search projection rather than raw rendered HTML. Searchable text includes structural rule fields, comments, rule notes, tag names, canonical UFW command text, and compatible known-host aliases. Each match produces structured evidence describing which field/filter matched. The UI can therefore show a compact reason or bounded text context without recomputing search semantics in Razor.

Filtering and ordering are mutually exclusive. A filtered result is not the complete ordered firewall family, so reorder controls are disabled whenever the query is active. Query changes are likewise disabled while an ordering preview is staged. The interaction state makes this invariant explicit rather than leaving each button/drag handler to decide independently.

## Rule metadata

Rule metadata consists of optional notes and reusable tags attached to semantic rule identity. The browser uses the same metadata editor when editing a live rule and while preparing a new rule.

For an existing rule, metadata persistence is independent from firewall mutation. The server confirms the semantic rule is still live before writing metadata, then the browser reconciles the returned metadata into its loaded enriched snapshot.

For a new rule, metadata remains subordinate to firewall authority: the signed firewall mutation is completed and reconciled first. Only after the resulting semantic rule identity is known does the browser attach the prepared metadata. If that second operation fails, the user is told that metadata attachment failed while the successful firewall mutation remains successful.

Tags have stable UUID identity, so renaming or recoloring a tag can update loaded metadata and configured tag filters without changing filter meaning. The visual tag chip is shared across list/detail/editor surfaces so preview and persisted presentation follow the same rendering path.

## Known hosts and network interfaces

Known-host and network-interface inventories both help rule authoring, but they have different authority models.

Known hosts are ASP-owned aliases. The client can cache visible entries and use them for address completion/search context; selecting one immediately resolves to the literal canonical address/network used by rule semantics.

Network interfaces originate from daemon/host inventory and are reconciled into ASP metadata. The client uses the resulting inventory for authoring, but the daemon still verifies interface existence at mutation time. The browser cache is therefore never the final authority for whether an interface can be used.

## Authentication and HTTP coordination

The authentication feature keeps the short-lived access token in memory and coordinates login, refresh, and logout across same-origin tabs. HTTP handlers attach browser credentials and bearer tokens to the appropriate API clients and can perform the controlled one-time refresh/replay behavior required by the rotating refresh-cookie model.

This coordination is deliberately below Razor pages. Components react to authentication state and call feature services; they do not implement refresh-token races or manually construct authorization headers.

## Presentation ownership

Razor pages coordinate user interaction but should not absorb reusable domain logic. Page code-behind owns lifecycle and composition; feature services own projections, validation, signing, mutation reconciliation, and other behavior that can be expressed without UI lifecycle state.

Component/page-specific Sass is colocated with its Razor owner. CSS isolation is opt-in for components whose rendered DOM is genuinely self-contained; styles that intentionally target MudBlazor-generated descendants or shared/portal markup remain globally compiled but colocated. The practical source conventions are documented in [Client UI development](../development/client-ui.md).

## Client invariants

The browser architecture should preserve these constraints as features evolve:

- authoritative firewall snapshots are immutable inputs to derived presentation state;
- stale snapshots may remain visible but are not mutation authority;
- family-local positions are presentation coordinates, not durable rule identity;
- filtering/search is derived and cannot silently change rule semantics;
- query/filter and ordering-preview modes do not overlap;
- authoring conveniences resolve to literal firewall semantics before signing;
- rule metadata is applied only to a live semantic identity and cannot make a firewall rule exist;
- API clients remain transport adapters rather than feature orchestration services;
- feature/application layers do not depend on Razor UI types;
- shared REST DTOs remain in `Ufw.Web.Model` rather than being duplicated in ASP/client projects.
