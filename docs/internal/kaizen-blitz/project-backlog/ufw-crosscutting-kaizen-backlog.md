# Cross-project Kaizen Follow-ups (October 2026)

These are new, **open** findings raised during the C3 client phase. They are separate from the 75-item original daemon/Web/client inventory; the [overall plan](../ufw-kaizen-plan.md) owns their sequencing and closure. They must not be silently treated as complete when the original three project waves finish.

## CROSS KZ-01 (P2): Targeted namespace cohesion and fanout audit

**Scope:** All projects, including tests and public DTOs. Some namespaces group unrelated responsibilities, and large flat namespaces can hide meaningful subdomains. Review high-fanout namespaces by **responsibility and dependency direction**, not by an arbitrary maximum class count.

**Initial navigation candidates** (rough `.cs` file counts on the approved C3.2 baseline, including code-behind but excluding generated `obj`/`bin`):

| Namespace | Files | Question to investigate |
|---|---:|---|
| `Ufw.Systemd.Firewall.Ordering` | 38 | Can planning, execution, recovery, and persistence have clearer internal subdomains? |
| `Ufw.Shared.Parsing.Parsers` | 37 | Do generic combinators and domain grammar implementations warrant separate cohesive subnamespaces? |
| `Ufw.Web.Client.Features.Rules.Metadata` | 34 | Can authoring, catalog, and cleanup responsibilities be navigated independently? |
| `Ufw.Web.Client.Features.Rules` | 31 | Can projections, state, and workflow primitives be grouped by ownership? |
| `Ufw.Systemd.Firewall` | 27 | Distinguish mutation infrastructure from read model and diagnostics, where helpful. |
| `Ufw.Shared.Domain` | 26 | Check algebra/evaluation vs basic packet-space model boundaries without reversing dependency direction. |
| `Ufw.Web.Client.UI.Components.Rules` | 23 | Check which reusable components belong to existing focused Rules subareas. |

The number alone is not evidence of poor design (for example, generated migrations, model DTO sets, and homogeneous parser combinators may remain flat).

**Method and acceptance:**

1. Inventory namespace membership, source folders, responsibility, accessibility, coupling, and external references (including tests, DI, generated source and serialization/reflection contracts). Note namespaces with unrelated types or unusually broad consumer fanout.
2. Propose small, coherent subdomains where navigation/encapsulation materially improve; avoid directory-only moves and class-count quotas. Where possible, move implementation details to `internal` as part of the existing C4 public-surface review, not ahead of it.
3. Apply moves in bounded batches per subsystem, using explicit namespace declarations and corresponding `using` changes. Protect HTTP DTO namespaces, localization resource lookup, source-generator assumptions, NativeAOT JSON/reflection registration and public API contracts.
4. Build/test affected projects and search for stale type/namespace references, resource names and DI registrations; update architecture docs only where a meaningful boundary changes. Report intentionally flat namespaces and why they remain unchanged.

**Sequencing:** after C4 CLIENT KZ-21/public surface and major structural migrations, or in a nearby domain-specific cleanup if no further churn would be introduced. A namespace move is not a reason to perturb an otherwise stable contract.

## CROSS KZ-02 (P2; P1 for production validation/intent concerns): Review clone-analysis findings

**Input:** User-provided clone report, October 10, 2026 (1 exact, 23 strong, 34 medium, 32 weak groups across daemon, Web, client, shared code, integration tests, and unit tests). Matches are **candidates, not confirmed harmful duplication**. Many are deliberate symmetric rule-operation tests or repeated setup that improves test readability. Track decisions by cluster rather than mechanically deduplicating every match.

| Candidate cluster | Report group(s) | Proposed disposition |
|---|---|---|
| IPC execution integration host setup in batch-delete, ordered-insertion, reorder, replacement tests | Exact 1; Strong 23; Weak 1-2, 4 | **Investigate first.** Extract a focused shared integration test host/builder if it maintains visibility of each scenario and preserves daemon/request configuration differences. |
| EF-backed test host/identity setup in Web tests | Strong 1, 11; Medium 33 | Consider shared test fixture for genuinely identical persistence/bootstrap mechanics; preserve explicitly test-specific data and lifecycle. |
| `SignedRuleIntentRequestValidator` delete/insert/replace validation | Strong 2 | **Correctness-sensitive.** Check one shared signed-envelope validation primitive with operation-specific payload binding; preserve exact field/error semantics and intentional differences. Coordinate with existing signed-intent boundary design. |
| Intent binder/canonicalizer/contract similarity across insert/replace/reorder/batch delete | Strong 19; Medium 15, 20, 22-24 | Audit existing shared primitives before extraction. Avoid a generic signed-operation framework that conceals operation-specific canonical bytes or validation. Add byte-level regression coverage for any refactor. |
| Small related assertions and repeated authorization/replay/partial-execution tests | Strong 3-9, 12-17, 20-22; Medium 1-10, 16-19; Weak 3, 5 and others | **Mostly intentional.** Keep named scenarios independent where repetition makes the safety contract testable and readable; extract fixture mechanisms only where they have a single invariant. |
| Repeated disposal, UI confirmation, metadata/template refresh, mapping setup and DI registration | Strong 10, 18; Medium 9-10, 21, 28-29; Weak 6-12 | Evaluate after existing lifecycle/dialog/API work, especially CLIENT KZ-14 and KZ-20-22. Do not build parallel abstractions ahead of those items. |
| Formatting, parser and set-algebra shapes | Medium 11-12, 16; Weak 13, 17, 28 | Verify whether structural repetition corresponds to different algebra/grammar/formatting invariants; only share proven common semantics. |

**Method and acceptance:**

1. Locate every reported cluster on the **current** source baseline; discard false/stale positives and record covered source items (original backlog IDs or this cross-cutting item).
2. Classify as intentional, already-covered/subsumed, bounded extraction, or needs distinct semantic tests. Prioritize repeated **production** correctness logic and high-cost integration-fixture setup, not raw clone counts.
3. Make changes in focused patches, with regression tests for intentional variation, and avoid generalizing unrelated operations merely because method bodies resemble each other.
4. Close only after documenting an explicit disposition for each **material** cluster. Test-only repeated assertion bodies need not be extracted if they aid scenario independence.

**Sequencing:** after the scheduled local cleanup that overlaps each cluster (C3/C4 for client UI, final contract freeze for signed intent); independently review IPC test host setup when touching those integration suites. This is a cross-project follow-up, not an interruption to C3.3 styling work.
