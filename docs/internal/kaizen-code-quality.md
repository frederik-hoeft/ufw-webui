# Kaizen blitz: code-quality pass

## Objective

Bring the current codebase to a reviewable steady state before the later test-coverage and documentation phases. This phase is intentionally behavior-preserving: improve source organization, dependency boundaries, consistency with `code-style.md`, and maintainability without changing the externally visible firewall, REST, IPC, authentication, or CLI contracts.

The clean `main` snapshot is the baseline. All work for this phase lives on `refactor/kaizen-code-quality` and remains unmerged until review.

## Baseline

- Use the supplied .NET 10.0.400 SDK and supplied NuGet package archive through an isolated offline toolchain.
- Restore with all external package sources disabled.
- Establish the unchanged baseline build and test result before refactoring.
- Treat generated outputs, EF migrations, and source-generator products as generated artifacts rather than manually restyling them unless generation itself is wrong.

Baseline result: offline restore succeeds, the full solution builds with zero warnings and zero errors, and all 462 tests pass.

## Plan

### 1. Inventory source organization and style

Review every handwritten C# and Razor source file against `code-style.md` and the repository architecture documentation. In particular:

- identify Razor components with substantial state, lifecycle, validation, event, dialog, or mutation logic that belongs in code-behind files;
- identify static helper/factory classes and distinguish pure value/protocol helpers from stateful, policy-bearing, or extensible behavior that benefits from DI;
- inventory REST and daemon controllers whose API metadata is mixed with implementation logic;
- check file/type organization, visibility, sealing, field/member order, explicit types, async naming, BCL keyword aliases, Unicode literals, whitespace, and import ordering;
- inspect wrapping manually, because formatter defaults do not express the repository's preference to avoid premature 80/120-column wrapping and normally allow lines up to roughly 190-200 characters;
- distinguish deliberate abstractions and protocol primitives from temporary workarounds before refactoring them.

### 2. Separate Razor markup from substantial component logic

Move substantial `@code` blocks into matching `.razor.cs` partial classes. Keep markup-focused components with only small parameter declarations or trivial presentation expressions inline; the goal is clearer responsibility boundaries, not mechanically doubling the file count.

Preserve component routes, DI bindings, parameters, disposal behavior, lifecycle behavior, and rendered output. Fix analyzer/correctness issues exposed when code moves from generated Razor output into normal C# only when the intended behavior is unambiguous.

### 3. Separate API contract metadata from controller implementation

For each Web and daemon controller, create two partials:

- `MyController.api.cs`: controller/route/authentication/source-generator attributes, endpoint attributes, XML API documentation, and declarations needed to describe the API surface;
- `MyController.cs`: constructor/dependencies and endpoint implementation logic without documentation/attribute clutter.

Keep routing, generated daemon endpoint discovery, authorization, response contracts, and tests behaviorally unchanged.

### 4. Replace meaningful static runtime helpers with DI services

Review every static helper identified by the inventory. Convert only helpers that own runtime construction, state, policy, or replaceable behavior. Keep pure canonicalization, identity, constants, formatting primitives, extension methods, and test-only fixture helpers static when an injected service would provide no meaningful seam.

The known `Ufw.Mock.Commands.CommandRuntime` factory is a prime candidate: command handlers should receive their runtime/executor dependencies through ConsoleAppFramework's DI integration rather than reconstructing them through a static factory on every command path.

Review result: `CommandRuntime` was the only runtime-construction helper that represented a meaningful DI seam and has been removed. Command categories now receive `UfwCommandExecutor` through ConsoleAppFramework DI, while the executor receives its options, state store, and parser as injected dependencies. Remaining static helpers are pure protocol/canonicalization/value operations, constants, extension registration, compiler helpers, or test fixtures; converting them would add indirection without useful state, policy, or substitution semantics.

### 5. Apply repository-wide handwritten-source cleanup

Use analyzers/formatter output as a cross-check, then manually review rules not encoded by those tools. Fix clear violations without broad unrelated rewrites. Specifically verify:

- existing authoritative `src/.editorconfig` aligns with `code-style.md`, including modifier ordering and the project's approximately 200-column wrapping guidance;
- no trailing whitespace or accidental repeated blank lines remain;
- imports are outside file-scoped namespaces and alphabetized;
- handwritten source uses ASCII plus Unicode escapes where needed;
- `Task`-returning production methods carry `Async`, with framework/test contracts reviewed rather than renamed blindly;
- internal/private types are `static` or `sealed` unless inheritance is required;
- fields are grouped at the top in the documented order;
- long signatures/expressions are wrapped only when structurally clearer, not because of conventional 80/120-column formatter limits;
- file names and top-level type organization are coherent.

### 6. Reconcile internal documentation

Update internal implementation documentation where this pass removes a known workaround or changes the component model. Remove completed open-task entries only when the corresponding cleanup is actually finished. Keep architecture documentation focused on steady-state behavior rather than a change log.

### 7. Validate and prepare review artifact

Run, offline and without implicit package acquisition:

1. solution restore against the source-less NuGet configuration when dependency graph changes require it;
2. full solution build with zero warnings/errors;
3. full solution test suite;
4. formatter/analyzer verification against handwritten source, excluding generated EF migrations where appropriate;
5. repository-level whitespace/encoding/line-length/manual style checks;
6. `git diff --check`;
7. full branch-diff review for accidental behavioral or generated-file changes.

Commit the coherent phase, produce a patch relative to local `main`, verify that it applies cleanly, and leave the feature branch unmerged for review.

## Completion status

The code-quality phase is complete and behavior-preserving validation is green.

- 24 Razor components/pages with meaningful state, lifecycle, validation, event, dialog, or mutation behavior now use matching `.razor.cs` partials. Four markup-oriented components retain small inline parameter/presentation blocks intentionally.
- All four ASP REST controllers and all three daemon controllers use `.api.cs` contract partials, leaving the plain controller partials focused on dependencies and endpoint implementation.
- `Ufw.Mock.Commands.CommandRuntime` has been removed. Mock command categories receive `UfwCommandExecutor` through ConsoleAppFramework DI, and runtime dependencies are registered once for the CLI invocation.
- Contract/model/parser files with unrelated top-level types or generic filename mismatches were split/renamed to follow the repository file/type rules.
- Handwritten source has no lines over 200 characters, non-ASCII characters, trailing whitespace, tab indentation, repeated blank lines, `var` declarations, or `this.` qualification. Remaining multiline signatures/calls were reviewed and retained only where structural grouping improves readability.
- The remaining internal/private non-sealed classes are intentional inheritance bases. The remaining `Task`-shaped name exceptions are framework/delegate constructs rather than ordinary async methods.
- `src/.editorconfig` now reflects the modifier ordering documented in `code-style.md` and the approximately 200-column line-length convention.
- `dotnet format` whitespace and style verification pass for handwritten source with generated EF migrations excluded. The normal solution build runs the configured project analyzers and completes with zero warnings; an additional whole-solution `dotnet format analyzers` verification was attempted but is not used as a gate because it does not complete within the sandbox execution limit.
- Final offline solution build: zero warnings, zero errors.
- Final test suite: 462 passed, zero failed, zero skipped.
