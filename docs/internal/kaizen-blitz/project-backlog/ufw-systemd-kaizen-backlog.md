# Ufw.Systemd Kaizen Blitz Review

## Post-D acceptance safety follow-up

See [ACC-01](../ufw-kaizen-plan.md#post-w-acceptance-remediation-october-2026).

- [x] Assess authoritative snapshots for duplicate semantic identities (comments do not distinguish identities); expose affected occurrences on reads.
- [x] Reject **all** signed firewall mutations at the daemon gate before nonce consumption/UFW writes when the snapshot is ambiguous. Return the stable `firewall.state.ambiguous` IPC error code; do not add an ASP-wide management-write guard or permit Web-based repair operations.
- [ ] Run signed IPC/Ufw.Mock duplicate-reorder and daemon-restart acceptance, including distinct-comment duplicates and zero-write/zero-journal assertions. Keep unresolved recovery fail-closed.

## Wave D completion status

**Status: complete.** All 29 findings in this inventory were resolved during Wave D. The problem/evidence sections below intentionally remain as the historical audit baseline; references to removed types or pre-refactor behavior describe the state that motivated the work, not the current implementation. The dependency-driven execution order and cross-project handoff are tracked in the [overall kaizen plan](../ufw-kaizen-plan.md).

The final daemon decisions that later phases should treat as fixed inputs are:

- configuration is loaded once at startup and is immutable for the process lifetime; configuration shape validation is pure and host/environment validation is a separate startup concern;
- the daemon server supports startup-selected `pipe` and `tcp` transports with one transport-neutral TLS/mTLS policy. The shipped production topology selects the Unix-domain pipe; the current Web IPC client is still pipe-oriented, so end-to-end TCP deployment requires Web-side client transport wiring during the Web phase;
- unexpected worker failure is process-fatal and delegated to systemd restart, while expected peer/I/O/protocol failures remain connection-scoped;
- durable daemon state uses shared durable-file primitives; nonce publication is persistence-first and replay storage is compacted;
- authoritative firewall reads return one explicit success/failure result whose success payload is the mapped `RuleListResponse`; process execution, diagnostics, snapshot matching, insertion placement, and reorder reinsertion cost are centralized primitives;
- normalized observed-state equality is distinct from semantic rule identity, and unsupported reorder rows are immutable rather than merely expensive to move;
- signed-intent v2 verification separates stable envelope/security checks from typed operation payload binding; authorized keys are snapshotted at startup and signature verification stays inside the key store; common gate/recovery/nonce choreography has one orchestrator;
- reorder and replacement retain explicit operation-specific recovery state machines behind smaller preflight/transaction components; post-mutation reconciliation using `CancellationToken.None` remains intentional once UFW may already have changed state;
- middleware composition is immutable, endpoint invocation/serialization is centralized, request timing is exception-safe, logging has one abstraction/console sink, and parser cleanup preserves opaque numbered UFW rows through a grammar-owned row-number fallback.

## Overall assessment

`Ufw.Systemd` does **not** need a broad rewrite. Most small abstractions and domain types are reasonably focused. The debt is concentrated in a few areas:

1. Firewall mutation workflows independently reimplement the same authoritative-snapshot/process/reconciliation protocol.
2. Reorder and replacement executors have become oversized transactional state machines.
3. Configuration has an ambiguous runtime lifecycle: it exposes "reload" but multiple consumers cache configuration-derived state indefinitely.
4. File-backed security/recovery components duplicate durability mechanics, and `FileNonceStore` has a concrete persistence-consistency hazard.
5. There are a handful of resource/lifecycle problems in transport and request logging.
6. Some transport/configuration abstractions are speculative or dead and should either become real extension points or be removed.

The kaizen should therefore extract **small shared safety primitives** while keeping operation-specific state machines explicit. A single generic "firewall transaction engine" would be a step backward.

---

# Inventory

## P1: correctness/reliability and high-value architecture

### KZ-001 — Make nonce consumption persistence-consistent

**Files:** `Security/Intent/FileNonceStore.cs`

`ConsumeUnsynchronizedAsync` adds the nonce to `_consumed` and `_expirations` before `AppendAsync` durably succeeds. If the append fails after the store has loaded successfully, the running process considers the nonce consumed while the durable store does not. A restart can then make the nonce usable again.

Expired records are pruned from memory on each consume, but the file is only rewritten during initial load. Long-running daemons therefore accumulate expired records indefinitely.

**Fix:** make persistence and in-memory publication one atomic logical operation. Either persist first and publish to memory only after success, or roll back the in-memory mutation on failure. Add bounded/threshold-based compaction so expired records are eventually removed from disk. Add failure-injection tests specifically for append failure after successful initial load.

### KZ-002 — Fix named-pipe stream leaks on setup/accept failure

**Files:** `Transport/Pipes/Unix/UnixNamedPipeServerStreamDescriptor.cs`

`CreateServerStream` constructs the `NamedPipeServerStream` and then calls `File.SetUnixFileMode`. If chmod fails, the stream is leaked. `ServeAsync` likewise does not dispose the newly created stream if `WaitForConnectionAsync` throws or is canceled.

**Fix:** transfer ownership only after setup/accept succeeds; dispose the stream in all exceptional paths.

### KZ-003 — Give request logging exception-safe timing/lifecycle behavior

**Files:** `Api/Middleware/RequestLoggingMiddleware.cs`

The stopwatch is not stopped/released when downstream middleware throws or cancellation propagates. The custom `ConcurrentBag<Stopwatch>` pool adds concurrency complexity for a tiny object, and its count-based cap is not a strong invariant under concurrency.

**Fix:** remove stopwatch pooling and use `Stopwatch.GetTimestamp` / `Stopwatch.GetElapsedTime`, or at minimum wrap the timing lifecycle in `try/finally`. Log failed/canceled requests distinctly if desired.

### KZ-004 — Define one authoritative firewall snapshot result type

**Files:** `Firewall/FirewallRuleSnapshotReadResult.cs`, `Firewall/FirewallRuleSnapshotReader.cs`, `Firewall/FirewallRuleQueryService.cs`, plus all mutation executors.

`FirewallRuleSnapshotReadResult` is a three-nullable-field pseudo-union (`Error`, `Snapshot`, `Configuration`). Callers repeatedly rely on `Error is null` and then use null-forgiving operators for the other fields. Mutation executors then independently convert the same pair into `RuleListResponse` through duplicate `TryReadSnapshotAsync` helpers.

**Fix:** use explicit success/failure variants. The success variant should expose the domain-level authoritative snapshot used by mutation logic, ideally already mapped to the representation consumers actually need. This removes impossible states and the repeated `read.Error is null ? ... read.Snapshot! ...` pattern.

### KZ-005 — Extract common UFW mutation execution primitives

**Files:**
- `Firewall/Deletion/FirewallBatchDeleteExecutor.cs`
- `Firewall/Insertion/FirewallOrderedInsertionExecutor.cs`
- `Firewall/Replacement/FirewallRuleReplacementExecutor.cs`
- `Firewall/Ordering/FirewallReorderExecutor.cs`
- `Firewall/Ordering/RuleReorderRecoveryCoordinator.cs`

These classes independently implement variants of the same protocol:

`read authoritative state -> verify precondition -> execute UFW -> reconcile with CancellationToken.None -> compare exact postcondition -> classify process failure/cancellation/state uncertainty -> combine diagnostics`.

Concrete duplication includes `TryReadSnapshotAsync`, `ProcessExecution`, `FormatProcessDiagnostic`, `CombineDiagnostics`, and snapshot-order matching.

**Fix:** extract narrow shared primitives, for example:

- authoritative snapshot provider/result;
- UFW operation runner that converts `ChildProcessException` + `UfwProcessResult` into a common process-execution result;
- common diagnostic formatting/combination;
- exact snapshot/order comparison utilities.

Keep batch-delete, insertion, replacement, and reorder state machines separate. Do not create one generic transaction executor.

### KZ-006 — Decompose `FirewallReorderExecutor`

**Files:** `Firewall/Ordering/FirewallReorderExecutor.cs` (476 LOC)

This class currently owns preflight classification, planning, move execution, journal management, cancellation reconciliation, recovery dispatch, order comparison, pending-plan derivation, occurrence mapping, and insertion command construction.

**Fix:** retain `FirewallReorderExecutor` as the orchestration façade, but split out at least:

- reorder preflight/classification;
- single-move executor;
- insertion-placement resolution;
- shared snapshot/process/reconciliation primitives from KZ-005.

`RuleReorderPlanner` itself is algorithmically cohesive and should not be folded into the executor.

### KZ-007 — Decompose `FirewallRuleReplacementExecutor`

**Files:** `Firewall/Replacement/FirewallRuleReplacementExecutor.cs` (348 LOC)

This class combines preflight, in-place update, insert-then-delete replacement, rollback, authoritative-state reconciliation, result classification, process diagnostics, and snapshot conversion.

**Fix:** split preflight from the replacement transaction and its rollback/reconciliation path. Reuse the process/snapshot primitives from KZ-005. Preserve the explicit replacement state machine rather than hiding it behind generic transaction callbacks.

### KZ-008 — Deduplicate signed mutation service choreography

**Files:**
- `Firewall/FirewallMutationService.cs`
- `Firewall/Deletion/FirewallBatchDeleteService.cs`
- `Firewall/Insertion/FirewallOrderedInsertionService.cs`
- `Firewall/Replacement/FirewallRuleReplacementService.cs`
- `Firewall/Ordering/FirewallReorderService.cs`

Every service repeats the same high-level sequence: verify signed intent, return rejection, acquire `IUfwExecutionGate`, run safety recovery, consume nonce, return the same nonce-conflict response, then invoke the operation executor.

**Fix:** introduce a small signed-mutation orchestration helper that owns execution-gate entry, safety guard, and nonce consumption. Keep operation-specific intent verification and response mapping explicit. Avoid a reflection/generic-heavy framework.

### KZ-009 — Resolve configuration lifecycle ambiguity

**Files:** `Configuration/ConfigurationImpl.cs` and all consumers of `IConfiguration.Settings`, especially `Network/NetworkApplication.cs`, `Transport/Security/ServerTransportSecurityService.cs`, `Security/Intent/FileAuthorizedKeyStore.cs`, and file-backed security components.

The API is named `TryReloadAsync`, but the daemon only calls it during startup. Several consumers snapshot values or derived objects indefinitely (`NetworkApplication._maxWorkers`, cached SSL options/certificate, cached authorized keys), while other consumers read `Settings` on each request. A real reload would therefore produce a partially updated process.

**Fix:** choose one model explicitly:

- preferred unless hot reload is a real requirement: immutable startup configuration via `LoadAsync`, then remove the reload claim

Do not keep the current half-reload semantics.

### KZ-010 — Supervise network worker capacity

**Files:** `Network/NetworkApplication.cs`, `Network/NetworkApplicationWorker.cs`

Known per-connection failures are caught, but any unexpected exception faults that worker permanently. `NetworkApplication` starts a fixed set and awaits `Task.WhenAll`; it does not replace a failed worker. One unexpected bug can therefore silently reduce serving capacity for the remainder of the process.

The worker also has six catch clauses with identical handling.

**Fix:** make worker failure policy explicit. Either restart failed workers/supervise the pool, or fail the daemon so systemd restarts a process whose invariants are no longer trusted. Collapse expected connection exceptions through a shared exception filter/classifier.

---

## P2: structural cleanup and coupling reduction

### KZ-011 — Separate configuration shape validation from environment validation

**Files:** `Configuration/Model/AppSettings.cs`, `PipeOptions.cs`, `SecurityOptions.cs`, `NetworkOptions.cs`, `IRequireValidation.cs`

Configuration model objects directly call `File.Exists`/`Directory.Exists`/OS APIs. `AssertIsValid()` returns `bool`, but invalid implementations throw and valid implementations return `true`, so the return value communicates a contract that does not really exist.

**Fix:** make model validation pure and deterministic, and perform filesystem/environment checks in a startup validator. Change the API to `Validate()`/`ThrowIfInvalid()` or return an actual validation result. This also makes configuration tests independent of the host filesystem.

### KZ-012 — Remove configuration drift and dead options; use safe diagnostics defaults

**Files:** `Configuration/Model/NetworkOptions.cs`, `Configuration/Model/PipeOptions.cs`, `Configuration/Model/AppSettings.cs`, `appsettings.default.json`, `Api/Framework/ApiExceptionMapper.cs`

Concrete drift/debt:

- `NetworkOptions.RequestTimeout` defaults to 30 seconds, while `appsettings.default.json` says 30 minutes.
- `PipeOptions.PipeName` defaults to `/run/ufw-systemd.pipe`, while the JSON uses `/var/run/ufw-systemd.pipe`.
- `WriteToConsole` exists in configuration but is not consumed by `Ufw.Systemd`.
- the default JSON enables `debug_mode`, and `ApiExceptionMapper` sends full exception text/stack details to clients when debug mode is enabled.

**Fix:** define defaults in one place or make the JSON authoritative; remove `WriteToConsole` or wire it intentionally; separate logging verbosity from "expose exception details on the wire" and make detailed remote errors opt-in/local-development-only.

### KZ-013 — Consolidate durable file persistence mechanics

**Files:** `Security/Intent/FileNonceStore.cs`, `Security/Intent/FileDeploymentIdentityProvider.cs`, `Firewall/Ordering/FileReorderRecoveryJournal.cs`, partially `FileAuthorizedKeyStore.cs`

Multiple components independently implement parent-directory creation, temp files, `WriteThrough`, synchronous `Flush(true)`, replace/move behavior, and filesystem exception handling.

**Fix:** introduce a narrow durable-file primitive (atomic write/replace, durable append where required) and, if useful for deterministic failure testing, a small filesystem seam. Keep domain serialization/formatting in each store.

### KZ-014 — Split `IntentVerifier` into stable envelope verification and operation payload binding

**Files:** `Security/Intent/IntentVerifier.cs` (349 LOC), `Security/Intent/IIntentVerifier.cs`, related intent result types.

`IntentVerifier` owns envelope/version/deployment/operation/nonce checks, JSON binding, operation-specific contract validation, normalization, canonicalization, authorized-key lookup, signature verification, timestamp checks, and construction of every accepted operation result.

Its private `PayloadVerification` is another nullable-field pseudo-union (`Canonical?`, accepted payload?, error?).

**Fix:** split stable cryptographic/envelope verification from per-operation typed payload validators/canonicalizers. Replace nullable pseudo-unions with explicit accepted/rejected result variants. A small operation strategy registry is reasonable; a generic reflection-based dispatcher is not.

### KZ-015 — Keep authorized-key ownership inside the key store

**Files:** `Security/Intent/FileAuthorizedKeyStore.cs`, `Security/Intent/IAuthorizedKeyStore.cs`, `Security/Intent/IntentVerifier.cs`

`TryGetKey` exposes cached mutable/disposable `ECDsa` instances owned by the singleton store. The store also loads only once; if the keys file is absent on first access, it caches an empty set until restart.

**Fix:** expose an operation such as `VerifySignature(keyId, data, signature)` or an immutable key representation rather than handing out owned cryptographic objects. Align key reload/rotation behavior with the configuration lifecycle decision in KZ-009.

### KZ-016 — Unify insertion placement logic

**Files:** `Firewall/Insertion/FirewallOrderedInsertionExecutor.cs`, `Firewall/Ordering/FirewallReorderExecutor.cs`, `Firewall/Ordering/RuleReorderRecoveryCoordinator.cs`

The code independently decides whether to use `ufw insert` vs `ufw add`, translates family-local placement to UFW numbering, and computes the expected global insertion position. Keeping those calculations separate risks semantic drift.

**Fix:** introduce one insertion-placement resolver that takes the authoritative snapshot/family/anchor and returns both the command placement and expected resulting position.

### KZ-017 — Separate command execution from output parsing

**Files:** `Interop/Commands/IUfwCommand.cs`, `IUfwCommand<T>`, all concrete UFW commands, `Interop/IO/UfwRunner.cs`, `Firewall/FirewallRuleSnapshotReader.cs`

Every command must implement `SetOutput(string)`, even mutation commands that do nothing with output. `UfwListCommand` stores mutable output and requires a later `GetResultAsync` call, which creates hidden lifecycle ordering and makes the command object stateful/non-reusable.

**Fix:** split executable command arguments from response parsing, or make query commands explicitly generic and have `IUfwRunner` return the parsed result. Mutation commands should not need no-op output methods.

Also consider adding a process-success property that includes cancellation; `UfwProcessResult.Succeeded` currently means only `ExitCode == 0`, forcing callers to repeatedly write `Succeeded && !CancellationRequested`.

### KZ-018 — Make transport security transport-neutral or name it pipe-specific

**Files:** `Transport/Security/ServerTransportSecurityService.cs`, certificate-validation handlers, transport modules.

`ITransportSecurityService` is generic, but the server implementation directly reads `configuration.Settings.Pipe` and caches pipe TLS options/certificate forever. That prevents clean reuse by another transport and participates in the configuration-reload inconsistency.

**Fix:** inject a dedicated immutable TLS/server-security options snapshot (or explicitly rename the service as pipe security). Define certificate rotation/reload semantics rather than caching accidentally.

### KZ-019 — Remove or finish the dormant TCP server transport

**Files:** `Transport/Tcp/*`, `DefaultServiceProvider.cs`, `Transport/Security/CertificateValidation/DefaultRemoteCertificateValidationHandler.cs`

The default service provider imports only the pipe transport module. The TCP module is not wired, and `TcpServerStreamDescriptor` ignores its injected configuration and hardcodes loopback port 1234. The server-side `DefaultRemoteCertificateValidationHandler` is also not wired; the provider registers `MutualTlsRemoteCertificateValidationHandler`.

**Fix:** if TCP is not a supported daemon transport, delete this dead surface. If it is intended to be supported, make transport selection explicit in configuration/DI and give TCP real endpoint settings/tests. Do not leave a hardcoded prototype transport in production source.

**Wave D resolution:** TCP is an officially supported daemon-server transport selected at startup alongside `pipe`, behind a delegating transport service. The shipped production topology remains pipe-based; Web-side TCP client selection is deferred to the Web daemon-gateway/composition work.

### KZ-020 — Deduplicate endpoint invocation/exception serialization

**Files:** `Api/Framework/UfwEndpointMapping\`1.cs`, `UfwEndpointMapping\`2.cs`, `UfwEndpointMappingBase.cs`

The requestless and request-bearing endpoint mappings duplicate endpoint invocation, cancellation filtering, exception mapping, and response serialization. `InitializeControllerAsync` is a no-op extension hook.

**Fix:** extract a common protected invocation/serialization primitive and keep only request binding/payload-presence checks in the specialized mappings. Remove the no-op initialization hook if no actual controller initialization contract exists.

### KZ-021 — Simplify middleware composition

**Files:** `Api/Middleware/RequestResponsePipeline.cs`, `RequestMiddlewareBase.cs`, `IRequestMiddleware.cs`

The pipeline mutates singleton middleware instances exactly once via `Initialize`, making construction order/lifetime part of hidden mutable state.

**Fix:** prefer immutable delegate composition or pass `next` in middleware construction/invocation. This removes one-shot initialization state and makes middleware easier to instantiate/test independently.

### KZ-022 — Consolidate logging and remove bypasses

**Files:** `Services/Logging/*`, `Interop/IO/DefaultChildProcessRunner.cs`, `Commands.cs`, several call sites.

`ConsoleLogger.Scoped<T>(T owner)` ignores the owner and returns the same singleton typed logger as `Scoped<T>()`. Many call sites repeatedly create the same scope expression per log statement. `DefaultChildProcessRunner` bypasses the logger and writes debug/stdout/stderr directly to `Console`, while the unused `WriteToConsole` setting suggests an abandoned logging switch.

**Fix:** use one logging abstraction consistently, cache typed loggers on consumers, remove the redundant owner overload, and represent child-process diagnostics through logging levels/structured fields rather than direct console writes.

---

## P3: contained cleanup / clarity improvements

### KZ-023 — Reduce parser-to-grammar-name coupling

**Files:** `Interop/Output/Visitors/UfwListCommandResultRowVisitor.cs`, `Interop/Output/Grammars/UfwListCommandResultGrammar.cs`, endpoint syntax nodes/parsers.

The visitor determines source vs destination by asking syntax nodes whether they have parents named `SourceGroup`/`DestinationGroup`. That couples semantic interpretation to grammar structure/names.

**Fix:** carry endpoint role as typed semantic context, or use source/destination-specific syntax nodes so visitor behavior does not depend on string/group ancestry.

### KZ-024 — Remove parser debug side effects and duplicate row-number parsing

**Files:** `Interop/Output/Grammars/UfwListCommandResultGrammar.cs`, `Interop/Output/UfwStatusParser.cs`

The grammar emits `Debug.WriteLine(node.ToString())`. `UfwStatusParser` separately regex-parses a numbered row to retain `DisplayNumber`, then the full grammar parses the row number again. `int.Parse` on the preliminary match can also throw for absurdly large input instead of treating the line as unparsed.

**Fix:** remove the debug write, use `int.TryParse`, and share the row-prefix parse with the grammar if that can be done without losing the important behavior of retaining unparsed UFW rows.

### KZ-025 — Reassess the configuration resource-provider abstraction

**Files:** `Configuration/Providers/*`, `Configuration/ConfigurationImpl.cs`, `Interop/Configuration/UfwDefaultsReader.cs`

`ResourceProvider` is an extensible pipeline abstraction but currently has one concrete strategy. Its mutable global `PreferredStrategy` permanently routes subsequent resources through the first successful strategy with no fallback, which would be wrong if heterogeneous strategies are later added. It also reuses `IPipelineHandler`, coupling resource lookup to IPC pipeline ordering. Meanwhile `UfwDefaultsReader` bypasses this abstraction and uses `File.ReadAllTextAsync` directly.

**Fix:** either simplify configuration loading to direct filesystem access, or define a real resource-provider contract with per-resource fallback and use it consistently. Do not retain speculative generality with incorrect future semantics.

### KZ-026 — Clarify normalized rule equality naming

**Files:** `Firewall/Ordering/FirewallRuleSemanticComparer.cs`

The comparer includes `Comment` in equality, while other project concepts such as semantic rule identity intentionally exclude comments. The implementation is useful, but the name `SemanticComparer` invites incorrect reuse.

**Fix:** rename it to communicate exact normalized/state equality, for example `FirewallRuleStateComparer` or `NormalizedFirewallRuleComparer`.

### KZ-027 — Remove small API friction in the execution gate

**Files:** `Firewall/IUfwExecutionGate.cs`, `UfwExecutionGate.cs`, `Firewall/Ordering/FirewallReorderRecoveryService.cs`

The gate only offers `RunAsync<TResult>`, so void-like operations have to manufacture a dummy return value.

**Fix:** add a non-generic `Task RunAsync(Func<CancellationToken, Task>, CancellationToken)` overload.

### KZ-028 — Make rule specification copying resilient to model growth

**Files:** `Firewall/FirewallMutationExecutor.cs`

`CloneWithAddressFamily` manually copies every `FirewallRuleSpecification` member. This is easy to forget when the model grows.

**Fix:** use an immutable record/`with`-style model if feasible, or centralize copying/projection in a shared helper owned by the model layer.

### KZ-029 — Make reorder planner cost semantics explicit

**Files:** `Firewall/Ordering/RuleReorderPlanner.cs`, classification code that supplies keep priorities.

The planner is otherwise cohesive, but the "keep priority" derives from rendered UFW argument length, which is an opaque proxy for the cost of reinserting a rule. The custom `EmptyPriorities` read-only dictionary is also unnecessary boilerplate.

**Fix:** rename/document the value as reinsertion cost/keep weight and hide the policy behind an explicit cost provider. Replace the custom empty dictionary with a standard empty instance.

---

# Items that should *not* be generalized away

- `RuleReorderPlanner` contains a real weighted-LIS planning algorithm and deserves to remain a focused algorithmic unit.
- Post-mutation reconciliation using `CancellationToken.None` is generally intentional and safety-relevant: once UFW may have mutated state, authoritative reconciliation/recovery should not be abandoned merely because the caller canceled.
- `UfwStatusParser` deliberately preserves unparsed rows. That conservative behavior is important for firewall safety and should survive any parser cleanup.
- The separate operation-specific replacement/reorder/batch-delete state machines encode materially different recovery semantics. Share primitives, not the whole workflow.
- The many small records/interfaces under `Firewall`, `NetworkInterfaces`, and `Interop/Output` are mostly not problematic on their own; most should stay unless a parent refactor naturally removes them.

# Suggested kaizen sequence

## Phase A — correctness first

1. KZ-001 nonce persistence consistency + compaction.
2. KZ-002 named-pipe disposal.
3. KZ-003 request logging exception safety.
4. KZ-009 configuration lifecycle decision.
5. KZ-010 worker-failure policy.

## Phase B — establish shared firewall primitives

6. KZ-004 typed snapshot result.
7. KZ-005 process/snapshot/diagnostic primitives.
8. KZ-016 shared insertion placement.
9. KZ-008 signed-mutation orchestration.

## Phase C — shrink the hotspots

10. KZ-006 reorder executor decomposition.
11. KZ-007 replacement executor decomposition.
12. KZ-014 intent verifier decomposition.

## Phase D — infrastructure cleanup

13. KZ-011 through KZ-013 configuration/persistence cleanup.
14. KZ-017 through KZ-022 command/transport/API/logging cleanup.
15. KZ-023 through KZ-029 contained clarity work.

# Definition of done for the blitz

The blitz should leave the following invariants:

- one authoritative snapshot/result abstraction;
- one shared UFW process-execution/diagnostic abstraction;
- one insertion-placement implementation;
- one signed-mutation safety/nonce/gate choreography;
- operation-specific state machines that are shorter and visibly encode only their unique semantics;
- explicit startup-only vs runtime-reload configuration semantics;
- durable stores with consistent failure semantics and testable persistence boundaries;
- no dormant hardcoded TCP transport or dead configuration switches;
- no resource leaks on named-pipe accept/setup or request-logging failure paths;
- parser cleanup preserves conservative handling of unknown UFW output.
