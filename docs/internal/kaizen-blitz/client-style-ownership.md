# C3.3 client SCSS ownership inventory

**Baseline:** approved C3.2; updated for the C3.3 consolidation. This inventories every non-isolated Sass import in `Ufw.Web.Client/UI/Styles/app.scss`. Normal page/component styling should move to `.razor.scss` CSS isolation where the owner directly renders the target markup; cross-boundary MudBlazor portal hooks and deliberately shared primitives must remain global. A Sass source path by itself does not establish isolation.

## Deliberately global, shared, or cross-boundary styles

| Import | Why it stays global |
|---|---|
| `core/*` (`fonts`, `tokens`, `global`, `interaction`) | Document/application foundations, variables and generic interaction behavior. |
| `controls/*` (`action-menus`, `dialogs`, `inventory`, `mud`, `standalone`) | Reusable Mud/application behavior; `action-menus` owns the two portal menu shapes as one primitive. |
| `Components/Rules/RuleRowShared.scss` | Identical row position, drag-handle and unsupported-rule fragments rendered via child components in both desktop and mobile containers. Restricted to the two owner roots. |
| `Components/Rules/KnownHostField.scss`, `NetworkInterfaceField.scss` | Mixed: owner-local field styles plus **intentional** `MudAutocomplete` popover/option/no-items hooks. Split only with verified portal behavior and option rendering. |
| `Components/Rules/Metadata/RuleGroupOption.scss`, `RuleTagOption.scss` | Autocomplete/select-option presentation can be instantiated inside Mud popovers outside parent component scope. Check actual option ownership and generated markup before isolation. |
| `Components/Rules/UfwRuleCommandPreview.scss` | Styles both the preview component and the delete-rule summary; split the latter by owner before making preview CSS isolated. |

## Owner-local candidates for isolated `.razor.scss`

These have primarily owner-rooted selectors, so they are candidates rather than automatic moves. Inspect their rendered child-component markup before migration; use a narrow `::deep` wrapper only where justified.

| Area | Existing non-isolated imports | Next action |
|---|---|---|
| Layout | `Layout/MainLayout.scss`, `Components/Layout/AppBrand.scss`, `Components/Layout/PageHeader.scss` | Split owner markup from descendant Mud integration, then migrate where safe. |
| Rule workspace | `RuleEditor.scss`, `RuleFamilyWorkspace.scss`, `RuleListToolbar.scss`, `RuleDesktopTable.scss`, `RuleMatchContext.scss` | Verify table/toolbar/overlay descendant structure and isolate owner-specific markup. |
| Rule rows | `RuleDesktopRow.scss`, `RuleMobileCard.scss`, `RuleMobileContent.scss` | Keep desktop/mobile layouts separate; shared child fragments already use `RuleRowShared.scss`. Parent styling of child roots needs explicit scoping decisions. |
| Rule filter | `Filtering/RuleFilterEditorDialog.scss` | Isolate editor containers, retain only any proven popover hooks globally. |
| Rule metadata | `Metadata/ManageRuleTagsDialog.scss`, `ReconcileRuleMetadataDialog.scss`, `RuleDetailsPanel.scss`, `RuleKnownHostEndpoint.scss`, `RuleMetadataEditor.scss`, `RuleTagChip.scss`, `RuleTagList.scss` | Isolate owner-rendered roots selectively; preserve consumers of cross-boundary selector roots. |

**Not in the global-import inventory:** existing `.razor.scss` entries registered in `sasscompiler.json` (including page styles and `RuleDesktopContent`) already use the Blazor isolation pipeline and should not be migrated again.

## C3.3 changes and C3.4 gate

- Replaced duplicated `RuleActionsMenu.scss` and `KnownHostActionsMenu.scss` with `controls/_action-menus.scss`; kept the original owner-specific `Class`/`PopoverClass` names so Mud's portal behavior stays unchanged.
- Moved exactly identical desktop/mobile position, drag-handle, and read-only presentation selectors to `Components/Rules/RuleRowShared.scss`. Table-only and mobile-only layout/state selectors remain owned by their respective components.
- C3.4 may migrate the owner-local candidates in small coherent batches, but it must include Sass compilation registration, output stylesheet inspection, client publish and responsive/browser verification. Avoid a wholesale rewrite that introduces `::deep` everywhere merely to satisfy isolation mechanically.
