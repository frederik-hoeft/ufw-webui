# Client UI Development

This document describes source organization and styling conventions specific to `Ufw.Web.Client`. For the runtime model and rule-page state/projection architecture, see [Browser application architecture](../architecture/browser-application.md).

## Source organization

Use the client directories according to responsibility rather than dependency-injection mechanism:

```text
Ufw.Web.Client/
  Api/             typed REST clients and HTTP protocol mechanics
  Configuration/   browser runtime configuration
  Features/        client-side domain/application behavior
  Services/        genuinely domain-agnostic browser services
  UI/              Razor components/pages/layout/styles
```

`Api` resource namespaces mirror ASP REST resources. Pure request/response DTOs do not live in the client; add versioned wire models to `Ufw.Web.Model/V{N}/<Resource>` so ASP and Blazor compile against the same contract.

Feature-specific DI services belong with their feature. `Services` is reserved for capabilities such as clipboard, local storage, localization, theming, and general client error mapping. Razor component types and UI-specific catalogues belong under `UI` and must not leak into feature/API layers.

## Razor components and code-behind

Keep a component's markup and significant component logic together by basename:

```text
RuleEditor.razor
RuleEditor.razor.cs
RuleEditor.scss
```

Small markup-only components do not need an empty code-behind. Conversely, pages/components with substantial orchestration should not accumulate large `@code` blocks when a `.razor.cs` file makes the lifecycle and dependencies easier to navigate.

Reusable domain behavior that does not depend on Razor lifecycle/DOM state should normally be extracted into a feature service rather than moved from one component code-behind to another.

## SCSS ownership

UFWeb uses a hybrid Sass model because Blazor CSS isolation works well for self-contained markup but poorly when a component intentionally styles MudBlazor-generated descendants, portal content, or shared child structures.

Use `MyComponent.razor.scss` when the component owns enough of its rendered DOM for normal scoped selectors to work. Use `MyComponent.scss` when the style is still owned by that component but must intentionally cross component or framework-generated DOM boundaries. Reserve `UI/Styles/` for genuinely global primitives and explicit framework/application integration rules.

Do not choose CSS isolation if the result is a large collection of `::deep` escape hatches. Isolation is a tool for real component ownership, not a goal by itself. The filename therefore communicates both source ownership and whether Blazor's scoped-CSS transform participates in the build.

### Isolated SCSS

`AspNetCore.SassCompiler` does not automatically treat every colocated `.scss` file as isolated. Isolated `.razor.scss` files are explicit opt-ins in `sasscompiler.json`, where Sass compilation produces the corresponding `.razor.css` before Blazor's scoped-CSS pipeline runs.

`SettingsSection.razor.scss` is the reference implementation. The generated `.razor.css` is build output and must not be committed. Blazor then emits the scoped rules into `Ufw.Web.Client.styles.css`, which is referenced by `wwwroot/index.html`.

When adding another isolated component:

1. create `MyComponent.razor.scss` beside the component;
2. add an explicit Sass compilation entry targeting `MyComponent.razor.css`;
3. keep the generated `.razor.css` ignored/untracked;
4. build/publish and verify that `Ufw.Web.Client.styles.css` contains the scoped selector;
5. switch back to colocated global Sass if normal styling would require pervasive `::deep`.

### Colocated global SCSS

Global component/page styles are imported by `UI/Styles/app.scss`. Keep the file beside the Razor owner even though its selectors are globally emitted. This preserves source locality without pretending that the rendered DOM is isolated.

Prefer one meaningful component root and nested selectors over verbose styling-only class names. Add a short local role class when generated component markup makes structural selectors ambiguous; do not force selectors such as `> div` when a MudBlazor component can also render a `<div>` at that position.

## Validation

For client/UI changes, at minimum run the client build and tests. Changes to Sass build configuration or isolated styles should also be validated through a client publish so both global and scoped bundles are present in deployable output.

For repository-wide validation:

```bash
dotnet restore src/Ufw.slnx
dotnet build src/Ufw.slnx --no-restore
dotnet test src/Ufw.slnx --no-restore --no-build
```

Before committing generated-style changes, check `git status` and ensure `.razor.css`, compiled Sass output, `bin/`, and `obj/` artifacts are not tracked.
