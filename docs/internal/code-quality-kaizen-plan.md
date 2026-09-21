# Code-quality kaizen plan

This document is the working plan for the current code-quality phase. It is intentionally internal and should be removed or folded into permanent architecture/development documentation once the phase is complete.

## Baseline and validation

The supplied source archive was imported as the local `main` baseline and validated with the supplied .NET 10.0.400 SDK and isolated offline NuGet cache.

- Offline restore succeeds with all package sources disabled.
- Full solution build succeeds with zero warnings and zero errors.
- All test projects pass: 977 tests total.
- Generated EF migration files and generated build output are excluded from style cleanup.

## Inventory findings

### Frontend page orchestration and state

`Pages/Rules.razor.cs` is the clearest architecture hotspot. It currently combines authoritative inventory refresh, known-host refresh, rule-list projection, filtering projection, address-family selection, dialog lifecycle, metadata mutation, delete mutation, insertion navigation, reorder preview, reorder execution, user notifications, and several independent busy flags. The page has parallel booleans for delete, metadata, and reorder flows, so legal/illegal interaction combinations are implicit rather than modeled.

`Rules/RulesPageState.cs` is also misnamed for its actual responsibility: it is shared by the rules list and create-rule page and primarily models authoritative rule-inventory freshness. Its transition API is method-shaped (`BeginRefresh`, `AfterReorder`, `AfterMutationFailure`, and so on) rather than an explicit state-machine transition boundary.

`Pages/CreateRule.razor.cs` is another large page code-behind. Its size is partly justified by the ordered-insertion workflow, but it still directly coordinates inventory freshness, mutation reconciliation, mutation calls, metadata persistence, navigation, and UI state. The state model should be made explicit first; service extraction should only be added where it creates a focused testable boundary rather than a page-sized facade.

### Rule presentation and filtering projection

The rules page maintains list projection, IPv4/IPv6 query results, known-host query context, and family selection as separate mutable fields that must be refreshed in the correct order. This is presentation derivation rather than page-owned state and is a good focused DI-service boundary.

### Tag management

The tag manager renders a dedicated circular color preview even though the actual tag presentation is `RuleTagChip`. The editor should preview the real component with a transient `RuleTag` value so preview and production rendering cannot drift.

The metadata stylesheet contains several one-use descendant class names, especially in tag-management UI. These should be reduced to meaningful component roots plus semantic/nested selectors. Reusable roles such as the tag pill itself and metadata layout hooks should remain named classes.

### Namespace and service organization

The client currently mixes DI services and domain/view models across top-level namespaces such as `Rules`, `RuleOrdering`, `RuleInsertion`, `KnownHosts`, and `NetworkInterfaces`, while UI types live under `Components` and `Pages`. The existing hierarchy is understandable but service ownership is not consistently visible from the namespace.

For this phase, introduce a `Ufw.Web.Client.Services` root for application services touched by the refactor and group rule-list orchestration under `Ufw.Web.Client.Features.Rules.Services`. Keep domain/view-state records under `Ufw.Web.Client.Features.Rules` and UI components under the existing `Ufw.Web.Client.Components` / `Ufw.Web.Client.Pages` roots to avoid namespace churn that provides no architectural value. Future migrations can move other service areas when they are actively touched.

### Static helpers

Production static classes were inventoried individually. Most are legitimate pure algorithms, constants, extensions, parsers, protocol primitives, or immutable theme/configuration factories and should remain static. Examples include shared firewall normalization/identity helpers, Roslyn symbol helpers, parser utilities, `RuleTagColor` normalization, theme construction, and mock formatting/parsing algorithms.

No broad “convert every static class” change is justified. Application policy or orchestration discovered during implementation should move behind DI, while genuinely stateless algorithms should stay static.

### Long production files

The largest non-generated production files were reviewed by responsibility rather than size alone. The daemon reorder/insertion executors, intent verifier, shared atomic/threading primitives, rule normalization, IPC JSON serialization, and mock UFW parser/formatter are cohesive algorithms/protocol implementations. Splitting them solely to reduce line count would make navigation and invariants worse, so they are not refactor targets unless concrete mixed responsibilities are found while editing.

## Implementation plan

1. **Make authoritative rule inventory an explicit state machine.**
   - Rename the shared `RulesPageState` concept to `RuleInventoryState` and status to `RuleInventoryStatus` so ownership is accurate.
   - Replace ad-hoc transition methods with `MoveNext(RuleInventoryTransition)` and typed transition events for refresh, metadata updates, mutation results, reconciliation, and failures.
   - Keep transition validation centralized and preserve current stale/current semantics exactly.
   - Update both rules-list and create-rule flows and add focused state-machine tests.

2. **Model rules-page interaction state explicitly.**
   - Replace `_deleteDialogOpen`, `_deleting`, `_metadataDialogOpen`, `_metadataSaving`, and `_reordering` with a single `RulesPageInteractionState` state machine.
   - Express legal transitions for delete confirmation/execution, metadata dialog/save, and reorder execution.
   - Derive `IsBusy`, mutation eligibility, metadata eligibility, and ordering eligibility from state instead of Boolean conjunctions.
   - Add unit tests for legal transitions, invalid transitions, and derived capabilities.

3. **Extract rule-list presentation derivation into a focused DI service.**
   - Add `IRulesPageProjectionService` / `RulesPageProjectionService` under `Ufw.Web.Client.Features.Rules.Services`.
   - Return one immutable projection containing list projection, family-specific query results, and IPv6 availability; keep the user-selected family as page-owned interaction state.
   - Make the page own only authoritative inputs (inventory, query, known hosts, ordering preview, selected family) and ask the service to derive presentation output.
   - Unit test IPv4/IPv6 availability, selection reconciliation, query projection, metadata projection, and ordering-preview projection.

4. **Reduce page orchestration where a focused boundary already exists.**
   - Rework `Rules.razor.cs` around the state machines and projection service, removing duplicated refresh/projection bookkeeping and redundant busy checks.
   - Rework `CreateRule.razor.cs` to use the renamed inventory state machine and remove incidental state-transition logic from the component. Extract an additional DI service only if a coherent responsibility remains after that cleanup.
   - Keep security-sensitive private-key lifetime explicit in the page and continue clearing keys after use.

5. **Unify tag preview and production visuals.**
   - Render `RuleTagChip` for tag color/name preview instead of a dedicated swatch preview.
   - Remove the redundant preview CSS.
   - Keep the color generator as an injected service and color normalization as a pure static value utility.

6. **Clean component SCSS and markup.**
   - Replace one-off tag-manager descendant classes with one meaningful component root and semantic/nested selectors.
   - Apply the same rule to touched rule metadata/editor markup where a class exists only to target one descendant.
   - Keep classes that represent reusable roles/state or cross-component layout hooks.
   - Do not edit generated `wwwroot/css/app.css`.

7. **Clarify client service namespaces and composition registration.**
   - Place newly extracted rule-page services under `Ufw.Web.Client.Features.Rules.Services`.
   - Add a focused service-registration extension for rule-management services so `Program` remains composition-root code rather than a long flat registration list.
   - Move existing rule-management service registrations into that extension without hiding unrelated browser/auth/API setup.
   - Avoid directory/namespace churn for untouched areas.

8. **Repository-wide style and maintainability sweep.**
   - Re-scan all hand-written C#, Razor, and SCSS for >200-character lines, premature wrapping, one-off styling classes, misleading namespaces, static application helpers, and oversized mixed-responsibility types.
   - Fix concrete violations found by the sweep, but leave cohesive algorithms intact.
   - Run `git diff --check` and review the complete branch diff for accidental/generated changes.

9. **Validation and review artifact.**
   - Offline restore with package sources disabled.
   - Full solution build with `--no-restore` and zero warnings/errors.
   - Full solution tests with `--no-restore --no-build`.
   - Generate a patch against the local `main` baseline and keep the feature branch unmerged pending review.

## Completion notes

The phase was completed against the supplied baseline with the following outcomes:

- Replaced the shared `RulesPageState` bag/method API with `RuleInventoryState.MoveNext(RuleInventoryTransition)` and one-type-per-file transition/status/snapshot primitives. Both rule listing and rule creation now use the same inventory-freshness state machine.
- Replaced the rules page's delete/metadata/reorder Boolean matrix with `RulesPageInteractionState.MoveNext(RulesPageInteractionTransition)`, making mutually exclusive UI workflows and invalid transitions explicit.
- Added `IRulesPageProjectionService` / `RulesPageProjectionService` under `Ufw.Web.Client.Features.Rules.Services` so rule-list projection, family query projection, and IPv6 availability are derived together instead of maintained as parallel mutable page fields.
- Added `RuleManagementServiceCollectionExtensions` to keep rule-management composition together while leaving unrelated browser/auth/API setup visible in `Program`.
- Converted `ClientRuntimeConfiguration` from a static application-policy helper into an immutable runtime configuration object registered through DI and used by typed management API clients.
- Reused `RuleTagChip` for tag-editor preview and removed the independent color-preview implementation.
- Reduced styling-only DOM classes in the tag manager, reconciliation dialog, metadata details/editor, filter selector, rules/status summaries, match evidence, and metadata-management page. Component-specific styling now prefers meaningful roots plus semantic nested selectors; portal/state/layout hooks remain named where the DOM relationship cannot safely express the role.
- Applied the 190-200 character convention across hand-written C# where ordinary calls/declarations had retained 80/120-column wrapping. Structural multiline layout remains for long constructors, lambdas, object/collection initializers, parser/protocol structures, and similar cases where it improves readability. Generated EF migrations and generated `wwwroot/css/app.css` were intentionally not hand-edited.
- Re-reviewed production static classes and the largest source files. Remaining statics are composition entry points, constants/extensions, immutable factories, or cohesive pure algorithms. The large daemon reorder executor, atomic/threading primitives, mock UFW parser, and similar files remain cohesive and were not split merely to reduce line count.
- Added focused state/projection tests. The client test project increased from 232 to 241 tests.

Final validation with the supplied .NET 10.0.400 SDK and a NuGet cache rebuilt solely from the supplied package archive:

- source-less offline restore: passed;
- full solution build with `--no-restore`: passed with zero warnings and zero errors;
- full solution test run with `--no-restore --no-build`: 986 passed, 0 failed, 0 skipped;
- `git diff --check`: passed;
- no generated build output or compiled CSS is included in the working-tree changes.
