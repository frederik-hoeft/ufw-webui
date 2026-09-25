# Client style organization

`UI/Styles/app.scss` is the single global Sass entry point. Keep only styles that are genuinely global or intentionally integrate with framework-rendered DOM under `UI/Styles/`:

- `core/` contains fonts, design tokens, document-level defaults, accessibility helpers, interaction policy, and Sass mixins.
- `controls/` contains cross-component MudBlazor/dialog/inventory/standalone integration that has no single Razor owner.

Page/component-specific styling belongs beside the Razor UI it styles. Use one of two companion conventions:

- `MyComponent.razor.scss` opts that component into Blazor CSS isolation. Prefer this for reusable components whose relevant DOM is substantially self-owned.
- `MyComponent.scss` (or `MyPage.scss`) stays globally compiled through `UI/Styles/app.scss`. Use this when the component intentionally styles MudBlazor-generated descendants,
  portal content, render-fragment content, or several closely related sibling components and isolation would mostly introduce `::deep` selectors.

CSS isolation is explicitly opt-in per component. For each `.razor.scss` companion, add a matching source/target entry to `sasscompiler.json` under `Compilations` so
`AspNetCore.SassCompiler` produces the corresponding `.razor.css` before Blazor performs scoped-selector rewriting. Automatic folder-wide scoped Sass discovery stays disabled because it
also compiles ordinary colocated `.scss` files into redundant adjacent `.css` artifacts. The generated `.razor.css`, `wwwroot/css/app.css`, and `Ufw.Web.Client.styles.css` outputs are build
artifacts and must not be committed.

Responsive rules should normally live in the same companion file as the base selector they modify. Prefer one stable class on the owning page/component boundary and nest private descendants
beneath it. Use semantic elements (`header`, `footer`, `article`, and similar) or short state/layout hooks within that boundary instead of repeating the owner name in one-off descendant classes.
Keep globally descriptive classes when they are shared across components, represent state, or are required as framework integration hooks such as a MudBlazor popover class.
