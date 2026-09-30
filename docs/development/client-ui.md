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
RuleEditor.razor.scss
```

Small markup-only components do not need an empty code-behind. Conversely, pages/components with substantial orchestration should not accumulate large `@code` blocks when a `.razor.cs` file makes the lifecycle and dependencies easier to navigate.

Reusable domain behavior that does not depend on Razor lifecycle/DOM state should normally be extracted into a feature service rather than moved from one component code-behind to another.

## SCSS ownership

CSS isolation is the default for page- and component-owned styling. A Razor page or component is the smallest normal styling unit, including page-specific helper components: one-off rules belong beside that owner in `MyComponent.razor.scss`, not in another page's stylesheet or a feature-wide style bucket.

When several consumers need the same rendered structure or presentation, extract a reusable Razor component or an explicit global primitive instead of making one component's stylesheet an implicit dependency of another. Parent pages should style layout wrappers they own; a child component owns its internal presentation.

Use one meaningful owner root and nested Sass selectors where practical. Normal scoped selectors should cover markup emitted directly by the owner. A small, deliberate `::deep` selector is appropriate when local composition must reach a MudBlazor-generated descendant or child-component root. If `::deep` becomes pervasive, move the presentation responsibility into the child component or, for generic framework behavior, into the shared global style layer.

Customizations that intentionally change a generic MudBlazor/application control everywhere belong in `UI/Styles/controls/`. Do not independently restyle generic input, button, autocomplete, typography, or table behavior from individual pages.

Use non-isolated `MyComponent.scss` only as an explicit exception when a component intentionally owns cross-boundary styling that cannot be expressed cleanly through isolated markup or a shared global primitive. Such files remain colocated with their Razor owner, use owner-specific root classes, and are imported by `UI/Styles/app.scss`.

### Isolated SCSS

`AspNetCore.SassCompiler` does not automatically treat every colocated `.scss` file as isolated. Each `MyComponent.razor.scss` file is registered in `sasscompiler.json`, where Sass compilation produces the corresponding `MyComponent.razor.css` before Blazor's scoped-CSS pipeline runs.

Generated `.razor.css` files are build output and must not be committed. Blazor emits the scoped rules into `Ufw.Web.Client.styles.css`, which is referenced by `wwwroot/index.html`.

When adding a page or component with local styling:

1. create `MyComponent.razor.scss` beside the Razor owner;
2. add an explicit Sass compilation entry targeting `MyComponent.razor.css`;
3. keep the generated `.razor.css` ignored/untracked;
4. use a local wrapper plus a narrowly scoped `::deep` selector only when composition crosses into child/framework-generated markup;
5. build/publish and verify that `Ufw.Web.Client.styles.css` contains the scoped selector.

### Colocated global SCSS

A non-isolated component stylesheet is exceptional. Use it only when the component deliberately needs globally emitted selectors to style markup it does not own and CSS isolation would require broad escape hatches. Keep the file beside the Razor owner and import it through `UI/Styles/app.scss`.

Prefer owner-specific roots and nested selectors over globally generic class names. If a selector describes generic MudBlazor/application behavior rather than one component's composition, move it to the appropriate `UI/Styles/controls/` partial instead.

## Validation

For client/UI changes, at minimum run the client build and tests. Changes to Sass build configuration or isolated styles should also be validated through a client publish so both global and scoped bundles are present in deployable output.

For repository-wide validation:

```bash
dotnet restore src/Ufw.slnx
dotnet build src/Ufw.slnx --no-restore
dotnet test src/Ufw.slnx --no-restore --no-build
```

Before committing generated-style changes, check `git status` and ensure `.razor.css`, compiled Sass output, `bin/`, and `obj/` artifacts are not tracked.
