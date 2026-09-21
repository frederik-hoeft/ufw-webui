# Client organization refactor plan

## Goals

Reorganize the Blazor WebAssembly client around clear domain and infrastructure boundaries, rename the client assembly to `Ufw.Web.Client`, and colocate component/page SCSS with its owning
Razor UI without forcing CSS isolation where MudBlazor renders the relevant DOM outside the component scope.

## Target structure

- `Components/`, `Pages/`, and `Layout/` remain the primary UI roots.
- `Features/<domain>/` owns domain/application code and API contracts for Authentication, Known Hosts, Network Interfaces, Rules, and Status.
- `Infrastructure/` owns cross-cutting HTTP protocol mechanics, runtime configuration, intent plumbing, and serialization.
- `Services/` is reserved for genuinely domain-agnostic browser/application services such as clipboard, localization, storage, theming, and client error mapping.
- Rules insertion, ordering, metadata, filtering, presentation, authoring, and DI services live below `Features/Rules/` rather than as peer top-level namespaces.

## SCSS convention

1. Keep global Sass entry points and genuinely global primitives/integration rules under `Styles/`.
2. Move page/component-specific global SCSS beside its Razor owner as `MyComponent.scss` / `MyPage.scss` when selectors intentionally reach MudBlazor-generated descendants, portals, or
   shared child markup.
3. Use `MyComponent.razor.scss` only when the component owns the rendered DOM sufficiently for Blazor CSS isolation to work naturally without pervasive `::deep` selectors.
4. Configure explicit per-component scoped SCSS compilations in `AspNetCore.SassCompiler` and validate the generated `.razor.css` -> Blazor scoped stylesheet pipeline in build and publish
   output.

## Work plan

1. Rename `Ufw.Client` and `Ufw.Client.Tests` projects/assemblies/namespaces to `Ufw.Web.Client` and `Ufw.Web.Client.Tests`; update solution, build/deployment scripts, docs, and friend assembly
   references.
2. Move client domain code under `Features/`, shared runtime plumbing under `Infrastructure/`, and agnostic services under `Services/`; split the old `Api` root by ownership.
3. Update namespaces/usings and move localization resources so resource base names continue matching the new localization namespace.
4. Colocate component/page styles; keep only global primitives and intentional global integration styles under `Styles/`.
5. Opt a self-contained component into `.razor.scss` through an explicit Sass compilation; verify generated isolation selectors and published static assets.
6. Add/adjust tests for namespace-sensitive behavior, especially localization resource resolution.
7. Run offline restore, full solution build/tests, publish validation, stale namespace/path scans, `git diff --check`, and final diff review.

## Completion notes

- Renamed the client and client-test projects/assemblies/namespaces to `Ufw.Web.Client` / `Ufw.Web.Client.Tests` and updated solution, deployment, scripts, documentation, generated-asset paths,
  and friend-assembly references.
- Reorganized production code under `Features/`, `Infrastructure/`, and domain-agnostic `Services/`; the previous top-level `Api`, `Auth`, `Rules`, `RuleInsertion`, `RuleOrdering`, and similar
  roots are gone.
- Moved localization marker types and resources together under `Services/Localization` and added a German resource-resolution test.
- Colocated page/component Sass with Razor owners while retaining only global primitives and intentional framework integration styles under `Styles/`.
- CSS isolation is opt-in per component through explicit `sasscompiler.json` `Compilations`. `SettingsSection.razor.scss` exercises the path without requiring `::deep`; ordinary colocated
  `.scss` continues to flow only through `Styles/app.scss`.
- Offline restore succeeded with package feeds disabled; the full solution builds with 0 warnings/errors; all 987 tests pass. Debug publish contains both `css/app.css` and
  `Ufw.Web.Client.styles.css`, and the isolated bundle contains the expected scoped `SettingsSection` selector.
- Stale `Ufw.Client` namespace/path scans and `git diff --check` are clean.
