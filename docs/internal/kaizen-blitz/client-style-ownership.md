# Client SCSS ownership inventory

**Baseline:** C3.5 (following the approved C3.4.1 parser patch). This inventories every remaining non-isolated Sass import in `Ufw.Web.Client/UI/Styles/app.scss`. Normal page/component styling should move to `.razor.scss` CSS isolation where the owner directly renders the target markup; cross-boundary MudBlazor portal hooks and deliberately shared primitives must remain global. A Sass source path by itself does not establish isolation.

## Deliberately global, shared, or cross-boundary styles

| Import | Why it stays global |
|---|---|
| `core/*` (`fonts`, `tokens`, `global`, `interaction`) | Document/application foundations, variables and generic interaction behavior. |
| `controls/*` (`action-menus`, `dialogs`, `inventory`, `mud`, `standalone`) | Reusable Mud/application behavior; `action-menus` owns the two portal menu shapes as one primitive. |
| `Components/Rules/RuleRowShared.scss` | Identical row position, drag-handle and unsupported-rule fragments rendered via child components in both desktop and mobile containers. Restricted to the two owner roots. |
| `Components/Rules/KnownHostField.scss`, `NetworkInterfaceField.scss` | Mixed: owner-local field styles plus **intentional** `MudAutocomplete` popover/option/no-items hooks. Split only with verified portal behavior and option rendering. |
| `Components/Rules/Metadata/RuleDetailsPanel.scss` | Details layout owns content and action fragments, but its field styling also targets the child `RuleKnownHostEndpoint`. The cross-component selectors stay global until component ownership is split. |
| `Components/Rules/UfwRuleCommandPreview.scss` | Styles both the preview component and the delete-rule summary; retain one shared declaration until KZ-14 addresses delete-confirmation presentation. |

## Retained global component and layout styles

The remaining imports have concrete cross-boundary consumers. Migrating them without changing component ownership would require broad `::deep` selectors or duplicate rules. These are intentional global styles, not an invitation to mechanically isolate everything.

| Area | Existing non-isolated imports | Ownership reason |
|---|---|---|
| Layout | `Layout/MainLayout.scss`, `Components/Layout/AppBrand.scss`, `Components/Layout/PageHeader.scss` | The layout styles span pages, `AppBrand` has login/standalone specificity overrides, and page header actions/statuses are supplied via caller fragments. Do not silently change the cascade. |
| Rule workspace | `RuleEditor.scss`, `RuleFamilyWorkspace.scss`, `RuleDesktopTable.scss` | `RuleEditor` has a MudPaper root and descendant MudForm/Mud alert styles; workspace layout crosses a MudPaper surface and child list components; table cell rules target rows supplied by `ChildContent`/child components. Isolation would change those styling contracts. |
| Rule rows | `RuleDesktopRow.scss`, `RuleMobileCard.scss` | Row-group and sibling table selectors cross component boundaries; mobile styles also target child metadata/details and drag fragments. Preserve responsive layout and shared row primitives without broad `::deep` styling. |
| Rule metadata | `Metadata/RuleDetailsPanel.scss` | The details panel styles `RuleKnownHostEndpoint` child field structure and header/actions fragments; leave those cross-component rules global until the presentation contract changes. |

**Not in the global-import inventory:** existing `.razor.scss` entries registered in `sasscompiler.json` (including page styles and `RuleDesktopContent`) already use the Blazor isolation pipeline. C3.4 and C3.5 migrations belong to this set.

## Completed slices and remaining gate

- Replaced duplicated `RuleActionsMenu.scss` and `KnownHostActionsMenu.scss` with `controls/_action-menus.scss`; kept the original owner-specific `Class`/`PopoverClass` names so Mud's portal behavior stays unchanged.
- Moved exactly identical desktop/mobile position, drag-handle, and read-only presentation selectors to `Components/Rules/RuleRowShared.scss`. Table-only and mobile-only layout/state selectors remain owned by their respective components.
- C3.4 moved six owner-local components to `.razor.scss`: `RuleListToolbar`, `RuleMatchContext`, `RuleMobileContent`, `RuleMetadataEditor`, `RuleTagChip`, and `RuleTagList`. Their source files are registered in `sasscompiler.json` and removed from global `app.scss` imports. Narrow `::deep` hooks are limited to Mud-rendered icons, toolbar inputs, and buttons.
- C3.5 isolates `RuleFilterEditorDialog`, `ManageRuleTagsDialog`, `ReconcileRuleMetadataDialog`, `RuleKnownHostEndpoint`, `RuleGroupOption`, and `RuleTagOption`. The option components own their rendered HTML even when instantiated within MudBlazor popovers; only the distinct option **container** and no-items selectors remain global. The dialog styles apply to locally rendered containers, with narrow `::deep` rules for Mud buttons; the endpoint uses one narrow `::deep` for the Mud icon.
- KZ-13 is complete at source-ownership level: every remaining non-isolated import is deliberately retained for the documented shared or cross-component behavior. Reconsider these boundaries only with a substantive markup/ownership change (particularly KZ-14 for confirmation dialogs). Browser-level responsive and overlay verification remains a separate integration acceptance step; compiled scoped CSS alone does not establish visual equivalence. Do not mechanically introduce `::deep` everywhere merely to satisfy isolation.
