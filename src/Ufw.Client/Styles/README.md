# Client style organization

`app.scss` is the only global Sass entry point. Keep styles grouped by the UI boundary that owns them rather than accumulating selectors in one page-wide file:

- `core/` contains fonts, design tokens, document-level defaults, accessibility helpers, and interaction policy.
- `layout/` contains application-shell/navigation layout. Responsive shell behavior stays with the shell styles.
- `controls/` contains generic/reusable application controls and deliberate MudBlazor customizations. Prefer one file per control or closely-related control family.
- `components/` contains domain-specific reusable component styles, grouped by feature where useful.
- `pages/` contains page-level composition that does not belong to a reusable control or component.

Responsive rules should normally live in the same file as the base selector they modify. A page stylesheet should not reach into MudBlazor internals to repair a reusable component; keep such customization in the owning control stylesheet and document why the framework override is necessary.

The generated `wwwroot/css/app.css` is build output and is intentionally ignored by Git.

## Scoped component styles

Blazor supports CSS isolation through companion `Component.razor.css` files. Sass itself is not a native Blazor CSS-isolation input, but the `AspNetCore.SassCompiler` package used by this project can compile scoped SCSS before Blazor performs selector rewriting.

Scoped Sass is currently disabled in `sasscompiler.json`. Much of the application styling intentionally customizes markup emitted by MudBlazor child components; moving those rules to CSS isolation would require `::deep` selectors and a second stylesheet bundle without materially improving ownership. Prefer the modular global Sass tree for those controls. Scoped styles remain a reasonable option for future components whose rendered markup is substantially self-owned.
