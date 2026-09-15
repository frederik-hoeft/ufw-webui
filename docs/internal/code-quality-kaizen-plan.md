# Code-quality kaizen plan

This plan tracks the maintainability pass performed before the next rule-presentation sprint. It is temporary maintainer documentation and should be removed once the refactor is accepted and any steady-state guidance has been folded into permanent documentation.

## Baseline and inventory

The supplied `main` snapshot restores, builds, and tests fully offline with the provided .NET 10.0.400 SDK and NuGet package archive. The baseline solution contains 15 projects. The handwritten source surface is dominated by the client rule UI and the privileged firewall pipeline; generated EF migration designer files are not candidates for structural refactoring.

Repository-wide inspection covered C#, Razor, JavaScript, Sass, resource strings, project files, and existing architecture/style documentation. The main maintainability findings are:

- rule presentation and authoring logic is split between large Razor components and several unrelated static helper classes (`KnownHostSuggestions`, `OrderedRuleInsertionNavigation`, `FirewallRulePresentation`, and `FirewallRuleDefaults`);
- `RuleTable` builds family partitions, occurrence coordinates, duplicate-rule mutability, and ordering state inside the component even though those concepts are presentation projections that the next metadata-enrichment sprint will need to extend;
- `RuleEditor` owns reference-data loading/filtering and capability-sensitive validation in addition to form interaction;
- `CreateRule` owns query parsing, ordered-insertion context resolution, mutation submission, reconciliation, and page presentation in one code-behind;
- several long files are genuinely single-purpose algorithms or protocol implementations (`Atomic`, parser combinators, UFW parsing, reorder execution). They should not be split merely to reduce line count; changes there require a concrete responsibility boundary;
- application-layer static utilities in the mock and daemon are mostly pure formatting/mapping/parsing functions. They remain static unless introducing a service gives a real dependency/testability boundary rather than DI ceremony;
- Sass contains one-off descendant styling represented by verbose DOM classes, including the rule-editor interface warning list and simple heading/divider variants that can be owned by the parent component selector instead;
- a small number of resource strings describe implementation history such as "current backend/API" instead of steady-state behavior;
- `dotnet format ... style --verify-no-changes` is clean, while whitespace verification reports one mis-indented client test block and two C# files with the wrong configured encoding;
- the source audit found a small set of lines beyond the repository's 200-character limit. These should be wrapped only where the structure remains clearer than one long line.

## Implementation sequence

1. **Establish deterministic validation.** Keep the supplied SDK/package cache isolated outside the repository; use source-less NuGet restore, then build/test with `--no-restore`. Record baseline failures separately from refactor regressions.
2. **Introduce a rule-table projection boundary.** Extract rule-family grouping, occurrence/family positions, duplicate semantic-id mutability, and ordering eligibility from `RuleTable` into a focused injectable projection service with direct unit tests. Keep drag/drop and dialog interaction in the component. Design the projection so future ASP-backed metadata can be attached without teaching the Razor component how to reconcile raw daemon rows.
3. **Replace application-level rule helper statics where DI adds a useful boundary.** Convert ordered-insertion navigation/context resolution and known-host suggestion matching into injectable services. Keep true constants, protocol/domain algorithms, startup-only configuration helpers, and trivial CSS-token helpers static where DI would only add indirection.
4. **Narrow rule-editor responsibilities.** Move reference-data loading/filtering and capability-sensitive rule validation behind focused services. The component should own form interaction and rendered state, not inventory orchestration or validation policy. Add service-level tests for invariants currently reachable only through component behavior.
5. **Reduce rule-creation page orchestration.** Move ordered-insertion query/context interpretation and mutation-reconciliation decisions out of `CreateRule`; leave navigation, snackbars, and page-local interaction in the page. Reuse the extracted services rather than adding a page-sized "manager" service.
6. **Clean Sass ownership.** Nest component-private descendants under their owning root selector, remove one-off DOM classes that exist only for styling, and preserve named classes only where they represent reusable/stateful styling hooks or are required by MudBlazor overrides/responsive behavior.
7. **Normalize UI copy.** Replace changelog-style descriptions with steady-state statements while preserving operational/security meaning and keeping localized resource pairs aligned.
8. **Apply repository-wide style hygiene.** Fix the formatter-reported whitespace/encoding issues, audit handwritten source for >200-character lines and obvious premature wrapping, and keep generated migration/source-generator output untouched unless the source template itself is wrong.
9. **Review specialization hotspots.** Revisit the remaining longest handwritten production files after the extractions. Split only where two independently testable responsibilities remain; document why algorithmic single-purpose files are intentionally left intact.
10. **Validate and review.** Run offline restore only if the project graph changes, then full build, full test suite, `dotnet format` style/whitespace verification, `git diff --check`, and a full branch-diff review. Produce a patch against local `main` and leave the branch unmerged for approval.

## Completion record

The implementation sequence above has been completed on `refactor/code-quality-foundation`. The refactor intentionally favors responsibility boundaries that the next rule-metadata sprint can extend rather than introducing generic manager/facade services.

### Responsibility boundaries introduced

- `IRuleTableProjectionService` owns the derivation of family groups, occurrence identities, family-local positions, mutation eligibility, ordering eligibility, and staged position changes from the authoritative rule list. `RuleTable` now owns rendering and interaction only.
- `IRuleInsertionNavigationService` owns ordered-insertion URI construction and query-context validation. Query transport is represented by `RuleInsertionNavigationQuery` instead of being parsed ad hoc by the create-rule page.
- `IRuleMutationReconciliationService` owns semantic rule identity/reconciliation decisions after add/insert mutations.
- `IRuleEditorReferenceDataService` owns known-host/network-interface reference-data loading, visibility filtering, and unknown-interface classification.
- `IRuleEditorValidationService` owns domain/capability-sensitive rule-editor validation. `IRuleDraftFactory` owns canonical authoring defaults.
- `IKnownHostSuggestionService` owns known-host filtering, display-value conversion, selection resolution, and address-family compatibility.
- `RuleOrderingResultAlert` now owns rendering of reorder outcomes and operation reports instead of leaving that presentation embedded in the rules page.
- Rule authoring and presentation types were moved out of the Razor-component namespace into `Ufw.Client.Rules.Authoring` and `Ufw.Client.Rules.Presentation`. Page state now lives under `Ufw.Client.Rules`.

### Deliberate non-refactors

The repository-wide static/helper and long-file review found several large files where line count does not indicate mixed responsibility:

- `Ufw.Shared.Threading.Atomic` and `AsyncLock.Internals` are tightly coupled concurrency primitives. Splitting their algorithms across DI services would make invariants harder to inspect and would add indirection to hot paths.
- `FirewallReorderExecutor` is a safety-critical reorder state machine, but its policy dependencies are already separated into planning, reinsertability classification, recovery coordination, journaling, snapshot reading, rendering, and UFW execution services. The remaining code is the transaction orchestration itself; splitting individual state-machine phases into additional services would fragment the failure/recovery flow without producing independent responsibilities.
- `UfwRuleParser`, parser combinators, rule normalizers/identity helpers, mock visibility/comparison helpers, and source-generator symbol/emitter helpers are pure algorithms with no runtime dependencies or replaceable infrastructure. Static APIs remain appropriate there.
- `UfwOutputFormatter` is a deterministic pure formatter used only by the mock status service. DI would not improve isolation or testability because there is no external dependency or state to substitute.
- startup/service-collection extensions, immutable theme construction, protocol constants/canonicalizers, and framework extension methods remain static by design.

Application-level statics that did own replaceable policy or orchestration (`KnownHostSuggestions`, `OrderedRuleInsertionNavigation`, and rule-draft defaults) were removed in favor of injectable services. The presentation-only `FirewallRulePresentation` helper was folded into its owning component instead of creating a DI service for CSS-token selection.

### Sass and UI-copy cleanup

Component/page-private styling now prefers one stable owner class with nested semantic descendants. The rule editor, command preview, known-host/network-interface fields, settings/status UI, standalone panel, and rule-ordering preview were simplified accordingly. Known-host and interface inventory pages now share the reusable `inventory-card` layout instead of duplicating page-prefixed heading/loading/empty/table/mobile classes. The convention is recorded in `src/Ufw.Client/Styles/README.md`; framework integration hooks such as detached MudBlazor popover classes remain named classes where nesting cannot own the rendered element.

Changelog-style rule-authoring/ordering copy was rewritten as steady-state behavior in both English and German resources.

### Repository-wide style audit

The handwritten source audit now has:

- no lines longer than the configured 200-character limit;
- no ordinary `var` declarations;
- clean `dotnet format` style and whitespace verification;
- corrected configured encodings/whitespace where formatter verification previously reported drift;
- no `git diff --check` whitespace errors.

Long signatures that had been wrapped around 80/120 columns were returned to single lines where they remain below the project limit. Structurally meaningful multiline expressions, initializers, and argument lists remain multiline where that improves readability.

### Validation

Validation uses only the supplied .NET 10.0.400 SDK and the isolated 168-package NuGet cache with external package sources disabled. The final branch state has been validated with:

- `dotnet build Ufw.slnx --no-restore -m:1`: 15 projects, 0 warnings, 0 errors;
- `dotnet test Ufw.slnx --no-restore --no-build -m:1`: 863 passed, 0 failed, 0 skipped across all seven test projects;
- `dotnet format Ufw.slnx style --no-restore --verify-no-changes`;
- `dotnet format Ufw.slnx whitespace --no-restore --verify-no-changes`;
- repository scan for handwritten source lines over 200 characters and ordinary `var` declarations;
- `git diff --check`.

The client suite contains 152 tests after adding direct coverage for the new service boundaries and their invariants.
