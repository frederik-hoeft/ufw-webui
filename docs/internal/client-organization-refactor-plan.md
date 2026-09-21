# Client organization refactor plan

## Goals

Reorganize the Blazor WebAssembly client around explicit UI, REST API, domain-feature, configuration, and general-purpose service boundaries; rename the client assembly to `Ufw.Web.Client`;
and colocate component/page SCSS with its owning Razor UI without forcing CSS isolation where MudBlazor renders the relevant DOM outside the component scope.

The important architectural boundary is ownership rather than implementation mechanism: REST transport contracts belong to `Api`, client-side application/domain behavior belongs to
`Features`, reusable browser/application services belong to `Services`, and Razor presentation belongs to `UI`.

## Target structure

- `Api/` owns the browser-to-ASP REST boundary. General HTTP response/error/serialization infrastructure lives at the API root. Resource subnamespaces mirror ASP controllers (`Auth`,
  `Intent`, `KnownHosts`, `NetworkInterfaces`, `Rules`, `RuleMetadata`, `RuleTags`, and `Status`), and resource-owned browser DTOs live under each resource's `Model/` namespace.
- `Configuration/` owns immutable browser runtime configuration and other true client configuration concepts.
- `Features/<domain>/` owns client-side domain/application state and DI services. Features consume `Api` contracts but do not define REST DTOs or HTTP clients. Rule intent crypto/signing and
  signed-mutation orchestration live under `Features/Rules/Intent`; rule ordering application stays under `Features/Rules/Ordering`.
- `Services/` is reserved for genuinely domain-agnostic browser/application services such as clipboard, localization, storage, theming, and client error mapping.
- `UI/` owns the complete Razor presentation tree: `App`, `_Imports`, `Components`, `Layout`, `Pages`, and `Styles`.
- `Resources/`, `Properties/`, and `wwwroot/` remain project-level build/runtime resources rather than artificial application namespaces.
- There is no generic `Infrastructure/` namespace. Code that previously fit there is assigned to the boundary it actually serves (`Api`, `Configuration`, `Features`, or `Services`).

## API organization

The API hierarchy mirrors browser-visible ASP resources rather than client feature ownership:

```text
Api/
  ClientJsonSerializerContext.cs
  HttpResponseMessageExtensions.cs
  ApiProblemDetails.cs
  ApiProtocolException.cs
  ApiRequestException.cs
  IManagementApiHealthClient.cs
  ManagementApiHealthClient.cs
  Auth/
    IAuthApiClient.cs
    AuthApiClient.cs
    Model/
  Intent/
    IIntentContextApiClient.cs
    IntentContextApiClient.cs
  KnownHosts/
    IKnownHostApiClient.cs
    KnownHostApiClient.cs
    Model/
  NetworkInterfaces/
    INetworkInterfaceApiClient.cs
    NetworkInterfaceApiClient.cs
    Model/
  Rules/
    IRuleApiClient.cs
    RuleApiClient.cs
    Model/
  RuleMetadata/
    IRuleMetadataReconciliationApiClient.cs
    RuleMetadataReconciliationApiClient.cs
    Model/
  RuleTags/
    IRuleTagApiClient.cs
    RuleTagApiClient.cs
    Model/
  Status/
    IDaemonStatusApiClient.cs
    DaemonStatusApiClient.cs
```

The management health probe is a general API concern because `/api/health` is not owned by a versioned controller. Client-domain orchestration such as `IRuleMutationService` and
`IRuleOrderingService` must not be placed under `Api` merely because they eventually call an API client.

## SCSS convention

1. Keep the global Sass entry point and genuinely global primitives/integration rules under `UI/Styles/`.
2. Move page/component-specific global SCSS beside its Razor owner as `MyComponent.scss` / `MyPage.scss` when selectors intentionally reach MudBlazor-generated descendants, portals, or
shared child markup.
3. Use `MyComponent.razor.scss` only when the component owns the rendered DOM sufficiently for Blazor CSS isolation to work naturally without pervasive `::deep` selectors.
4. Configure explicit per-component scoped SCSS compilations in `AspNetCore.SassCompiler` and validate the generated `.razor.css` -> Blazor scoped stylesheet pipeline in build and publish
output.

## Work plan

1. Rename `Ufw.Client` and `Ufw.Client.Tests` projects/assemblies/namespaces to `Ufw.Web.Client` and `Ufw.Web.Client.Tests`; update solution, build/deployment scripts, docs, and friend
assembly references.
2. Reorganize domain/application code under `Features/` and domain-agnostic DI services under `Services/`.
3. Establish `Api/` as the sole REST boundary, split API clients by ASP resource, place resource DTOs under `Model/`, and move generic HTTP/JSON mechanics to the API root.
4. Remove the ambiguous `Infrastructure/` root: move runtime configuration to `Configuration/` and rule intent behavior to `Features/Rules/Intent`.
5. Unify Razor presentation under `UI/` and update namespaces/imports accordingly.
6. Move localization resources with their marker namespace so resource base names remain correct.
7. Colocate component/page styles under `UI`; keep only global primitives and intentional global integration styles under `UI/Styles/`.
8. Opt a self-contained component into `.razor.scss` through an explicit Sass compilation; verify generated isolation selectors and published static assets.
9. Mirror the production boundaries in the client-test tree where useful, and add/adjust tests for namespace-sensitive behavior such as localization resource resolution.
10. Run offline restore, full solution build/tests, publish validation, stale namespace/path scans, `git diff --check`, and final diff review.

## Completion notes

- Renamed the client and client-test projects/assemblies/namespaces to `Ufw.Web.Client` / `Ufw.Web.Client.Tests` and updated solution, deployment, scripts, documentation, generated-asset
  paths, and friend-assembly references.
- Reorganized client domain/application code under `Features/` and truly cross-domain DI services under `Services/`.
- Established the root `Api/` boundary with controller-shaped resource namespaces and `Model/` DTO namespaces. HTTP protocol errors/extensions and the source-generated client JSON context
  now live at the API root, while client-side rule mutation/ordering orchestration no longer masquerades as REST infrastructure.
- Removed `Infrastructure/`; moved runtime configuration to `Configuration/`, intent context transport to `Api/Intent`, and browser intent crypto/signing/mutation behavior to
  `Features/Rules/Intent`.
- Unified the Razor presentation tree under `UI/`, including `App`, `_Imports`, `Components`, `Layout`, `Pages`, and `Styles`.
- Moved localization marker types and resources together under `Services/Localization` and added a German resource-resolution test.
- Colocated page/component Sass with Razor owners while retaining only global primitives and intentional framework integration styles under `UI/Styles/`.
- CSS isolation is opt-in per component through explicit `sasscompiler.json` `Compilations`. `SettingsSection.razor.scss` exercises the path without requiring `::deep`; ordinary colocated
  `.scss` continues to flow only through `UI/Styles/app.scss`.
- Split UI filter-definition/catalog registration from `Features.Rules.Services`, so feature/API layers no longer depend on UI namespaces; the application composition root wires both boundaries explicitly.
- Offline restore succeeded with package feeds disabled; the full solution builds with 0 warnings/errors; all 987 tests pass. Debug publish contains both `css/app.css` and
  `Ufw.Web.Client.styles.css`, and the isolated bundle contains the expected scoped `SettingsSection` selector. Stale namespace/path scans and `git diff --check` are clean.
