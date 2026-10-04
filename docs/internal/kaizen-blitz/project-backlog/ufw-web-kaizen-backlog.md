# Ufw.Web Kaizen Blitz Review Inventory

## Wave D daemon handoff

The daemon phase is complete. Web work should consume the finalized daemon contract rather than reopening daemon implementation structure:

- daemon configuration is startup-immutable;
- the daemon server supports startup-selected pipe/TCP with transport-neutral TLS/mTLS, while the current `Ufw.Ipc.Client`/`Ufw.Web` composition and production topology remain Unix-pipe based; Web KZ-02/KZ-04 own any client-side transport selection required for TCP deployments;
- `RuleListResponse` is the authoritative ordered firewall snapshot; semantic `RuleId` excludes comments/transient numbering, while occurrence IDs are snapshot-local coordinates tied to the exact fingerprinted order;
- signed-intent v2 canonicalization/verification semantics are stable, authorized mutation keys are snapshotted at daemon startup, and mutation routes return typed transaction/recovery data that must not be collapsed to process success/failure;
- unexpected remote exception details are opt-in via `expose_remote_exception_details`; `debug_mode` controls local diagnostics only;
- daemon validation failures now carry stable string codes alongside property/message data; current producers populate them, while application-v1 readers tolerate older code-less payloads. The final Web error boundary must preserve those identities and repeated failures for the same property.

## Scope and method

This review covers the uploaded snapshot of `src/Ufw.Web` in full: **154 files** total, including **133 authored/non-migration files** and **21 EF Core migration files**. The migration set consists of migration implementations plus generated designer/snapshot artifacts. Generated EF files were reviewed for schema consistency and classified as generated/append-only rather than treated as normal refactoring targets.

The review focused on code smells, duplicated code/logic, layering and coupling, transaction boundaries, error handling, state/reconciliation flows, validation ownership, dead code, persistence efficiency, and API/controller orchestration. This is a source review, not a compiler-warning report. A local .NET 10 analyzer build was attempted with the supplied SDK/package bundle, but dependency restore did not complete, so no claim is made that the analyzer/build warning set is clean.

Some fixes identified in `Ufw.Web` naturally belong in the adjacent shared-model project. For the data-access/layering review, the current dependency flow was therefore also traced through `Ufw.Web.Model`, representative `Ufw.Web.Client` API/state consumers, and the daemon-facing `Ufw.Shared` contracts. Those projects are used to validate the target architecture, while the file-by-file ledger remains scoped to `Ufw.Web`.

## Executive summary

The project does **not** need a broad rewrite. Most individual classes are small and the core feature decomposition is recognizable. The main issue is boundary erosion as features accumulated:

- EF persistence entities are already server-private, but the current repository layer conflates database access, projection into shared client/server models, endpoint response-envelope construction, mutation error classification, and full-inventory refreshes;
- the shared `Ufw.Web.Model` types called `*Item`/`*InventoryItem` are already functioning as client/server domain/read models, while `*InventoryResponse` and mutation responses are endpoint-specific envelopes;
- request DTOs are passed into feature services that often exist only to trim/validate them and delegate once to a repository;
- daemon protocol/IPC concerns are split between adapters, services, and controllers;
- rule metadata reconciliation mixes protocol verification with application persistence;
- validation limits leak outward from EF entities into services, while request-shape checks are inconsistently performed manually inside business services instead of by the REST validation layer;
- local mutation-outcome/result types duplicate common persistence failure propagation and controller mapping;
- several EF queries materialize arrays even when callers only need a collection, paying an avoidable list-to-array allocation;
- several feature families have accumulated parallel implementations that are beginning to diverge.

There are also two concrete correctness fixes to make immediately (`KZ-14`, `KZ-15`) and several safe deletions/cleanups (`KZ-12`). No P0/stop-ship defect was identified in this pass.

### Validated current data-flow baseline

ASP is an **aggregation/enrichment layer**, not the sole producer of application state. The current code already has several distinct authoritative data sources:

| Area | Authoritative/source state | ASP-owned/enrichment state | Current composition path | Client-visible shape |
|---|---|---|---|---|
| Firewall rules | daemon `RuleListResponse` / `ListedFirewallRule` | PostgreSQL rule metadata (notes, tags, group) plus capture time | `RuleInventoryService` reads daemon snapshot, extracts rule IDs, queries metadata, builds `RuleInventoryResponse` | `RuleInventoryResponse` embeds the daemon firewall snapshot and metadata sidecar directly |
| Rule mutations | signed daemon transaction requests/responses | best-effort metadata cleanup/replacement reconciliation | `RulesController` invokes `IRuleDaemonGateway` and metadata coordination where required | daemon response types are returned directly, except replacement adds metadata-reconciliation sidecar |
| Network interfaces | daemon interface-name list | PostgreSQL public identity, comment, visibility, presence, reconciliation timestamp | `NetworkInterfaceInventoryService` coordinates the daemon gateway + `NetworkInterfaceDataAccess`; normal reads filter to present rows and stale metadata uses a separate cleanup contract | `NetworkInterfaceInventoryResponse` |
| Known hosts | PostgreSQL | DNS resolution/reconciliation state | `KnownHostService` coordinates DNS + `KnownHostDataAccess` | `KnownHostInventoryItem` collection |
| Rule tags/groups/templates | PostgreSQL | none external; templates embed shared firewall specification | V1 controllers validate/map requests and call domain-sliced DAL components; DAL returns shared read models/mutation facts | `RuleTagItem`, `RuleGroupItem`, `RuleTemplateItem` collections inside V1 envelopes |
| Rule metadata reconciliation | daemon live rule identities | PostgreSQL metadata rows | reconciliation service joins daemon identities with DB metadata in memory | orphan/reconciliation response |
| Authentication | ASP.NET Identity + PostgreSQL refresh-token state | JWT/cookie policy | authentication flow + refresh-token service | auth token response |
| Intent/status | daemon | none | controllers invoke narrow intent/status daemon gateways | daemon/shared response or no-content |

Two existing choices are important and should be preserved:

1. `Ufw.Web.Data.Model/*Entry` types are **persistence entities only**. They are internal and are not used by the client.
2. The existing data-bearing `*Item`/`*InventoryItem` types already behave much more like shared **management-domain/read models** than like EF/DAL entities. As each slice migrates, those pure data concepts move to `Ufw.Shared.Management`; `Ufw.Web.Model` remains the versioned HTTP DTO layer.

### Target data-access and model architecture

Use four deliberately different kinds of type instead of creating a DTO clone at every boundary:

```text
Persistence entity               Shared domain/read model              API contract
(Ufw.Web only)                    (client + server)                     (versioned HTTP)

KnownHostEntry  --EF projection--> KnownHost  -----------------------> KnownHostInventoryResponse
RuleTagEntry    --EF projection--> RuleTag    -----------------------> RuleTagInventoryResponse
RuleGroupEntry  --EF projection--> RuleGroup  -----------------------> RuleGroupInventoryResponse
                                  ^                                  + metadata/sidecars as needed
                                  |
                                  +-- may also be embedded directly in another domain model
```

- **Persistence entities** remain internal under `Ufw.Web.Data.Model`. They describe relational storage, can contain surrogate IDs/navigation properties, and never cross the DAL boundary.
- **Shared domain/read models** are data-only management concepts usable by both server and Blazor client. They live under `Ufw.Shared.Management`, physically separate from the HTTP contracts. The existing `RuleTagItem`, `RuleGroupItem`, `RuleTemplateItem`, `KnownHostInventoryItem`, `NetworkInterfaceInventoryItem`, and `RuleMetadataItem` migrate there slice-by-slice; no duplicate mapping model should be created merely to satisfy layering aesthetics.
- **Request DTOs** remain versioned transport types and carry Data Annotations/nullability required for untrusted JSON input. Before persistence/domain work, controllers or small request mappers convert them to valid normalized arguments or an internal command/query type. A new internal type is justified only when semantics actually change; for simple requests, spreading validated properties into DAL arguments is preferable to creating a 95%-identical object.
- **Response DTOs/envelopes** remain versioned HTTP contracts and may embed shared domain/read models directly, including collections. Endpoint-specific metadata, diagnostics, pagination, reconciliation state, or upstream daemon state live beside those models as sidecars. There is no per-element remapping allocation when the DAL already projected the desired shared model.
- **Authoritative upstream shared models** may also be embedded directly when appropriate. `RuleInventoryResponse.Firewall` is the existing example: ASP enriches the daemon snapshot instead of cloning the firewall model simply because it crossed an HTTP boundary.

For DAL organization, adopt a **domain-sliced CQRS-lite** structure rather than repository-per-table or one global repository:

```text
Data/Access/
  Auth/
  KnownHosts/
  NetworkInterfaces/
  Rules/
    Metadata/
    Groups/
    Tags/
    Templates/
```

Within a slice, distinguish specialized read queries from mutation/storage operations where useful (`*Queries`, `*Store`, or equivalently explicit query/command methods). The exact class count is secondary to these invariants:

- only the DAL performs direct `ApplicationDbContext`/`DbSet`/EF query work;
- `IQueryable` never escapes the DAL;
- SQL-capable filtering, joining, aggregation, ordering, and projection happen before materialization;
- query APIs are shaped around the information a use case needs, not around returning entire tables for higher layers to reconstruct relational joins in memory;
- a query may join across several tables/slices when that is the efficient way to answer one request;
- multiple SQL statements are acceptable when intentional (pagination counts, split-query avoidance, mutation checks, etc.); accidental N+1/query fan-out is not;
- direct EF projection into the shared domain/read model is encouraged for read paths;
- mutation methods return mutation facts/domain state and standardized error information, not an endpoint response envelope.

### Controller versus application-service rule

Do **not** require `Controller -> ApplicationService -> DAL` for every endpoint. The extra layer only earns its existence when it owns meaningful orchestration or reusable behavior.

Current examples that **do** justify an orchestration component include:

- rule inventory composition: daemon snapshot + metadata enrichment;
- rule metadata reconciliation: daemon liveness + DB cleanup;
- network-interface reconciliation: daemon discovery + persisted metadata;
- known-host DNS workflows: DNS + persistence + concurrency/reconciliation checks;
- authentication flows: Identity + refresh-token/JWT transaction workflow;
- rule replacement/delete workflows once daemon protocol interpretation and metadata reconciliation are separated cleanly.

Current examples that are largely **ceremonial after request validation/mapping is fixed** include `RuleGroupService` and `RuleTagService`; `RuleTemplateService` mostly consists of request normalization/domain validation followed by one repository call and should be reevaluated after KZ-05. For simple endpoints the controller may map validated transport input, call the relevant DAL query/store directly, and wrap the returned domain model in the HTTP response.

Controllers may own HTTP binding, request-to-domain mapping, status-code selection, and trivial response-envelope composition. They should not own EF queries, relational joins over separately materialized collections, transactions, daemon+DB mutation workflows, reconciliation policy, or domain invariants.


A concrete classification for the current components is:

| Current component | Target role |
|---|---|
| `RuleGroupService`, `RuleTagService` | remove after KZ-05; controller maps validated request and calls group/tag DAL query/store |
| `RuleTemplateService` | removed in W1.1; controller request mapping normalizes the template and applies `RuleSpecificationValidator` before calling the template DAL |
| `RuleInventoryService` | retain as cross-source inventory composer (daemon + metadata DAL), with a responsibility-specific name |
| `RuleMetadataReconciliationService` | retain as cross-source reconciler, but keep daemon protocol parsing outside it |
| `RuleMetadataService` | split: pure metadata command handling/reconciliation vs daemon protocol interpretation/gateway |
| `NetworkInterfaceInventoryService` | split pass-through CRUD from meaningful reconciliation; retain a reconciliation coordinator |
| `KnownHostService` | retain/rename as known-host command coordinator because DNS resolution and optimistic reconciliation are real workflow behavior |
| `AuthenticationFlowService` | retain as auth workflow coordinator; move refresh-token EF work behind the auth DAL slice |
| direct `IUfwClient` controller usage | replace with daemon gateway(s); controller remains HTTP adapter |

### Suggested blitz outcome

At the end of the blitz, direct EF access should be confined to a domain-sliced DAL; persistence entities should remain server-private; shared management-domain/read models should be projected directly by EF and reused by the client/API without clone DTOs; request DTOs should be validated at the REST boundary then mapped to non-null/normalized domain arguments; response envelopes should embed domain state plus sidecars rather than forcing redundant mappings; and dedicated orchestration services should exist only for real multi-step/cross-source workflows. Daemon protocol access and common persistence/HTTP error policy should also be centralized.

## Priority inventory

| ID | Priority | Finding | Primary payoff |
|---|---|---|---|
| **KZ-14** | P1-correctness | Fix known-host metadata validation being reported as an address error | Correct client diagnostics / invariant clarity |
| **KZ-15** | P1-correctness | Preserve multiple daemon validation errors for the same property | Correct API validation payloads |
| **KZ-01** | P1 | Establish a domain-sliced DAL and distinguish persistence entities, shared domain models, and API envelopes | Query efficiency, layering, no redundant DTO mappings |
| **KZ-23** | P1 | Remove ceremonial application-service hops; keep coordinators only for real workflows | Simpler dependency graph, clearer ownership |
| **KZ-02** | P1 | Create a consistent daemon gateway boundary and centralize IPC exception handling | Boundary consistency, less controller boilerplate |
| **KZ-03** | P1 | Split rule metadata persistence reconciliation from signed daemon protocol parsing | Lower protocol coupling, clearer reconciliation |
| **KZ-05** | P1 | Move REST request-shape validation out of business services and formalize request-to-domain mapping | Clean REST boundary, trusted domain inputs |
| **KZ-04** | P2 | Decompose `Startup` into feature registration and startup lifecycle units | Composition-root maintainability |
| **KZ-06** | P2 | Unify rule metadata/template tag and group dependency handling | Remove divergent relationship logic |
| **KZ-22** | P2 | Standardize mutation/error propagation instead of feature-local outcome plumbing for common failures | Reusable persistence/application error semantics |
| **KZ-07** | P2 | Reduce rule-group/rule-tag catalog copy-paste without generic-controller overengineering | Remove repeated CRUD plumbing |
| **KZ-08** | P2 | Centralize semantic rule-ID extraction from daemon snapshots | One semantic identity rule |
| **KZ-09** | P2 | Standardize HTTP error shape and declared response contracts | Stable API error contract |
| **KZ-10** | P2 | Make authentication transaction ownership explicit and reduce service contracts tied to `IdentityUser` | Explicit auth consistency model |
| **KZ-11** | P2 | Shrink accidental public surface area | Smaller assembly/API surface |
| **KZ-13** | P2-design | Decide whether transient interface disappearance is allowed to erase user-owned metadata | Prevent accidental metadata loss if persistence is intended |
| **KZ-12** | P3-quick | Delete dead validation/signing implementations and minor stale code | Dead-code/maintenance reduction |
| **KZ-16** | P3 | Extract auth cookie policy and Identity error mapping | Auth maintainability |
| **KZ-17** | P3 | Centralize daemon endpoint paths | Protocol route locality |
| **KZ-18** | P3 | Tighten bulk persistence operations after the boundary refactor | Set-based DB efficiency |
| **KZ-19** | P3-release-risk | Treat the description-length migration as explicitly destructive history | Migration safety discipline |
| **KZ-20** | P3-optional | Reduce repeated versioned-controller policy attributes only if conventions stay obvious | Minor declaration cleanup |
| **KZ-21** | P3-quick/perf | Prefer `ToListAsync` over `ToArrayAsync` for EF materialization when array identity is irrelevant | Avoid needless secondary allocations |

## Detailed findings

### KZ-14 [P1-correctness] Fix known-host metadata validation being reported as an address error

**Problem:** When name/comment normalization fails, create/update returns `KnownHostMutationOutcome.InvalidAddress`. The controller then tells the client that the address is invalid even though the failing input is metadata.

**Evidence:** `KnownHostService.cs:15-18` and `:44-47`; `KnownHostsController.cs:57-62` maps that outcome to an address-specific ProblemDetails.

**Refactor target:** Add a distinct `InvalidHost`/`InvalidMetadata` outcome, or move all metadata shape validation to a common validator that produces field-specific failures. Do not overload `InvalidAddress`.

**Status:** Completed through the request-validation path rather than another mutation outcome. W2.3 moved known-host name/comment constraints to raw-value Data Annotations backed by shared `KnownHostLimits`; W2.6 completed the literal/DNS address-configuration audit, so invalid literal addresses and invalid source/family combinations are rejected by model validation before `KnownHostService`. The old `InvalidAddress`/`InvalidDnsConfiguration` mutation outcomes are therefore gone.

**Primary files:** `Ufw.Web.Model/V1/KnownHosts/KnownHostRequest.cs`, `Services/KnownHosts/KnownHostService.cs`, `Api/V1/Controllers/KnownHostsController.cs`

### KZ-15 [P1-correctness] Preserve multiple daemon validation errors for the same property

**Problem:** Daemon validation errors are assigned one-by-one into `ValidationProblemDetails.Errors`. Multiple errors for the same property overwrite each other, so clients receive only the last message.

**Evidence:** `DaemonApiErrorMapper.cs:20-23` performs `problem.Errors[error.PropertyName] = [error.ErrorMessage]` for each error.

**Refactor target:** Preserve every validation failure as an independent structured public entry rather than collapsing by property. Keep deterministic producer order and carry the stable machine-readable code when the daemon supplied one.

**Status:** Completed in W2.7.1. Public validation failures now use an ordered `validationErrors` ProblemDetails extension. The daemon mapper copies every `ModelValidationError` entry independently, so repeated failures for one property no longer overwrite each other and S1 stable validation codes survive the HTTP boundary. Dedicated regression coverage locks down duplicate-property ordering and codes.

**Primary files:** `Api/V1/Errors/DaemonApiErrorMapper.cs`, `Api/V1/Errors/ApiProblemDetailsFactory.cs`, `Ufw.Web.Model/V1/Errors/*`

### KZ-01 [P1] Establish a domain-sliced DAL and distinguish persistence entities, shared domain models, and API envelopes

**Problem:** The current `*Repository` classes successfully centralize most non-auth EF access, but they combine too many responsibilities: SQL construction, persistence mutation, projection, public response-envelope construction, feature-local error results, and usually a full-inventory re-query before a write transaction commits. At the same time, the shared `Ufw.Web.Model` data objects are already consumed directly by the Blazor client, so treating every repository projection as an HTTP DTO leak would drive the project toward a wasteful `EF entity -> DAL DTO -> application DTO -> response DTO` ladder.

**Historical evidence:** Before the vertical migrations, `KnownHostRepository`, `NetworkInterfaceInventoryRepository`, `RuleGroupRepository`, `RuleTagRepository`, and `RuleTemplateRepository` returned `*InventoryResponse` objects directly and commonly rebuilt a full inventory before commit. `RuleMetadataRepository` projected client-consumed models from the persistence layer, while `RefreshTokenService` was the notable direct-EF implementation still located under `Services/Auth`. Relational `*Entry` types themselves were already internal under `Data/Model`.

**Refactor target:**

1. Treat `Data/Model/*Entry` strictly as persistence entities. They never escape the DAL.
2. Formalize the data-only objects shared by server/client as **domain/read models**, not DAL entities. Reuse them directly in API responses and EF projections. Move the pure shared data concepts under `Ufw.Shared.Management` (with domain-specific subnamespaces), while keeping V1 request/response envelopes under `Ufw.Web.Model.V1`. Do not introduce structurally identical intermediate models solely to satisfy layering.
3. Keep versioned request/response contracts distinct from those shared models. Response envelopes may hold a domain object/list plus endpoint-specific sidecars. Existing `RuleInventoryResponse { Firewall, Metadata, CapturedAt }` is a good example of enrichment composition.
4. Move direct EF work into `Data/Access/<domain>` slices (`Auth`, `KnownHosts`, `NetworkInterfaces`, `Rules/Metadata`, `Rules/Groups`, `Rules/Tags`, `Rules/Templates`, etc.). Split read/query and write/store responsibilities when useful, but do not create repository-per-table plumbing.
5. Keep `IQueryable` private to the DAL. Specialized DAL queries should perform filtering, joining, ordering, aggregation, and projection in SQL before materialization. A query is allowed to span multiple tables when that best answers one request.
6. For writes, return mutation facts/domain state plus standardized errors. Do not build an HTTP response envelope inside the write transaction. Where the current API contract requires the refreshed full inventory, commit first and then run the normal read query; alternatively evolve the endpoint later to return only the changed domain state if that is a better contract.
7. Prefer direct EF projection into the shared domain/read model. Avoid tracked entity graphs and later in-memory projection unless persistence semantics actually require them.
8. Add architectural/query-regression tests: direct `ApplicationDbContext`/EF usage outside the DAL should fail an architecture check, and representative list/mutation paths should have bounded SQL-command counts so N+1/materialization fan-out cannot quietly reappear.

**W2.1/W2.3/W2.4/W2.5 status:** Rule metadata, known hosts, network interfaces, and refresh-token persistence now follow the final boundary. Shared management read models live under `Ufw.Shared.Management`; EF access is confined to matching `Data/Access` slices; response envelopes compose shared models without clone DTOs; and write workflows return mutation facts/domain state before post-commit inventory reads. Auth deliberately remains an application workflow coordinator, but refresh-token EF access now lives under `Data/Access/Auth` and participates in the transaction context owned by that coordinator. The overall KZ-01 item remains open only for the final architectural/query-regression checks.

**Primary files:** `Data/Access/Auth/*`, `Data/Access/KnownHosts/*`, `Data/Access/NetworkInterfaces/*`, `Data/Access/Rules/Groups/*`, `Data/Access/Rules/Metadata/*`, `Data/Access/Rules/Tags/*`, `Data/Access/Rules/Templates/*`; adjacent shared models under `Ufw.Shared.Management` and HTTP contracts under `Ufw.Web.Model`

### KZ-23 [P1] Remove ceremonial application-service hops; keep coordinators only for real workflows

**Problem:** Several endpoint paths currently follow `Controller -> *Service -> *Repository` even when the service has one implementation and, after basic request normalization, performs exactly one repository call. This indirection does not create a meaningful substitutable abstraction. It also encourages future logic to be split arbitrarily between controllers, services, and repositories instead of assigning ownership by responsibility.

**Evidence:** `RuleGroupService.GetAsync/DeleteAsync` and `RuleTagService.GetAsync/DeleteAsync` are direct pass-through calls; create/update primarily trim/check request fields and then delegate once. `RuleTemplateService` similarly owns request normalization plus semantic rule validation before one repository call. In contrast, `RuleInventoryService`, `RuleMetadataReconciliationService`, `NetworkInterfaceInventoryService.ReconcileAsync`, `KnownHostService` DNS workflows, and `AuthenticationFlowService` genuinely coordinate multiple sources/steps.

**Refactor target:** Do not mandate an application-service layer. Classify endpoint paths by behavior:

- **Simple query/CRUD:** controller performs HTTP binding + request mapping, calls a DAL query/store, then maps the standardized result to HTTP. No dedicated `FooService` is required.
- **Cross-source or multi-step workflow:** retain/introduce a specifically named coordinator/reconciler/flow component. It may depend on DAL slices, daemon gateways, DNS, Identity, or pure domain services as required.
- **Pure reusable domain behavior:** keep it in focused validators/normalizers/evaluators, not in a generic feature service.

Controller composition must stay shallow: it may combine independently meaningful results into a response sidecar, but it must never rebuild a relational query by joining/filtering separately materialized DB datasets. If that happens, create a specialized DAL query instead.

Likely removals/reclassifications after KZ-05/KZ-01: `IRuleGroupService`/`RuleGroupService`, `IRuleTagService`/`RuleTagService`, and potentially `IRuleTemplateService`/`RuleTemplateService`. Likely retained but renamed/tightened: rule inventory composition, rule metadata reconciliation/mutation coordination, network-interface reconciliation, known-host DNS coordination, and authentication flow.

**Primary files:** `Api/V1/Controllers/RuleGroupsController.cs`, `Api/V1/Controllers/RuleTagsController.cs`, `Api/V1/Controllers/RuleTemplatesController.cs`, `Services/Rules/IRuleGroupService.cs`, `Services/Rules/RuleGroupService.cs`, `Services/Rules/IRuleTagService.cs`, `Services/Rules/RuleTagService.cs`, `Services/Rules/IRuleTemplateService.cs`, `Services/Rules/RuleTemplateService.cs`, `Services/Rules/RuleInventoryService.cs`, `Services/Rules/RuleMetadataReconciliationService.cs`, `Services/NetworkInterfaces/NetworkInterfaceInventoryService.cs`, `Services/KnownHosts/KnownHostService.cs`, `Services/Auth/AuthenticationFlowService.cs`

### KZ-02 [P1] Create a consistent daemon gateway boundary and centralize IPC exception handling

**Problem:** Daemon access is split between typed source abstractions and direct `IUfwClient` calls from controllers. The same `try/catch UfwIpcException -> mapper -> StatusCode` plumbing is repeated in several controllers. Rules reads go through `IDaemonRuleSource`, while mutations bypass that boundary entirely.

**Evidence:** Direct IPC: `IntentController.cs:9-22`, `StatusController.cs:8-21`, `RulesController.cs:12-176`. Repeated mapping also appears in `RuleMetadataController.cs:11-50` and `NetworkInterfacesController.cs:17-33`.

**Refactor target:** Introduce feature-oriented daemon gateways, especially an `IRuleMutationGateway`/`IRuleDaemonGateway`, plus small status/intent gateways if needed. Move protocol route selection and IPC calls there. Handle `UfwIpcException` and invalid daemon responses once through ASP.NET exception handling/filter middleware, leaving controllers to map domain outcomes only.

**W1.2.1 status:** Gateway ownership is established. `IRuleDaemonGateway` owns authoritative rule reads and signed mutations; network-interface response validation is behind `INetworkInterfaceDaemonGateway`; status and intent use narrow dedicated gateways. Application controllers/services no longer depend on `IUfwClient`, and the unsigned rules/status/intent/network-interface paths are private gateway constants. Signed rule mutations continue to use the method/route already encoded by their shared request messages.

**W1.2.2 status:** Completed. `Ufw.Ipc.Client.TrySendAsync` now returns `UfwIpcResult`/`UfwIpcResult<T>` for daemon-declared application failures, so gateway error flow no longer depends on catching `UfwIpcException`; the existing `SendAsync` API remains as an assertive compatibility surface over the same result path. Gateways return `DaemonResult`/`DaemonResult<T>`, which preserve daemon application failure data for workflows that need `TryGetResult`, while `Result`/`EnsureSuccess()` create an exception only for the normal centralized path. `DaemonApiExceptionFilter` owns daemon exception-to-HTTP translation, so controller-local `try/catch UfwIpcException` plumbing and mapper dependencies are removed. Network-interface reconciliation uses the non-throwing result path to classify daemon failures as upstream unavailability; malformed IPC/daemon responses become `DaemonInvalidResponseException` at the gateway boundary rather than leaking generic `InvalidDataException`. The existing operation-specific cleanup/reconciliation exception handling was audited and remains local. Public ProblemDetails normalization, stable validation-code exposure, and same-property validation accumulation remain KZ-09/KZ-15 work rather than being changed here.

**Primary files:** `Api/V1/Controllers/IntentController.cs`, `Api/V1/Controllers/StatusController.cs`, `Api/V1/Controllers/RulesController.cs`, `Api/V1/Controllers/RuleMetadataController.cs`, `Api/V1/Controllers/NetworkInterfacesController.cs`, `Api/V1/Errors/DaemonApiExceptionFilter.cs`, `Api/V1/Errors/DaemonApiErrorMapper.cs`, `Services/Daemon/DaemonResult.cs`, `Services/Rules/RuleDaemonGateway.cs`, `Services/NetworkInterfaces/NetworkInterfaceDaemonGateway.cs`

### KZ-03 [P1] Split rule metadata persistence reconciliation from signed daemon protocol parsing

**Problem:** `RuleMetadataService` simultaneously validates user metadata, queries live daemon state, deserializes a signed replacement payload with the daemon JSON context, validates signed protocol contracts and semantic identities, interprets mutation responses, and performs database reconciliation. This is too many reasons to change and tightly couples application metadata to IPC wire details.

**Evidence:** `RuleMetadataService.cs:1-8` imports firewall, request/response, JSON serialization, and signed-intent namespaces. `ReconcileReplacementAsync` at `:44-90` parses/validates daemon protocol state before invoking persistence.

**Refactor target:** Move signed request/response interpretation into the rule daemon gateway and emit a small domain reconciliation command such as `(originalRuleId, replacementRuleId, originalStillLive)`. Keep metadata service focused on metadata invariants and persistence reconciliation.

**W1.3 status:** Completed. `RuleDaemonGateway` now interprets completed signed replacement requests together with the authoritative daemon response and emits `RuleReplacementReconciliationFacts` only when the signed replacement identity and final snapshot are coherent. Non-completed firewall outcomes explicitly require no reconciliation; completed-but-inconsistent protocol state is classified as reconciliation preparation failure without discarding the completed firewall report. `RuleMetadataService` consumes only the reconciliation facts and owns database reconciliation/failure handling, with no signed-payload deserialization or daemon response interpretation.

**Primary files:** `Services/Rules/IRuleDaemonGateway.cs`, `Services/Rules/RuleDaemonGateway.cs`, `Services/Rules/IRuleMetadataService.cs`, `Services/Rules/RuleMetadataService.cs`, `Api/V1/Controllers/RulesController.cs`

### KZ-05 [P1] Move REST request-shape validation out of business services and formalize request-to-domain mapping

**Problem:** Validation ownership is split across three layers. Application services depend on EF entity types purely to obtain max-length constants; similar limits exist independently in public request models; and several services manually reject basic request-shape failures inside normalization/business logic even though `[ApiController]` can reject them declaratively before the use case runs. `RuleTemplateService.TryNormalize`, for example, mixes trimming/normalization with name/description length checks, a null-rule check, metadata request checks, and semantic firewall-rule validation. Groups and tags follow the same general pattern. This obscures which failures are malformed REST input versus validly-shaped requests that fail domain semantics.

**Original evidence:** Before the boundary migrations, `RuleTemplateService` manually combined name/description lengths, null-rule checks, metadata shape, normalization, and semantic rule validation; group/tag services followed the same pattern, and multiple services consumed persistence-owned constants to enforce request shape. These types/locations are historical evidence rather than current implementation.

**Refactor target:** Separate transport input from trusted domain/DAL input without introducing a clone model by default:

- Put transport-shape constraints such as required values, string lengths, simple formats, valid collection presence, and obvious enum/range constraints on the versioned `Ufw.Web.Model` request contracts with Data Annotations (or equivalent centralized ASP.NET validation). `[ApiController]` should reject malformed JSON input before domain/DAL work begins.
- After transport validation, explicitly map raw request values to normalized/trusted arguments. For small requests, spread normalized properties directly into the DAL/domain call. Introduce an internal command/query record only when it represents a real semantic transition or is reused, such as the existing `RuleTemplateValues`/`RuleMetadataValues` concepts.
- Internal domain/query inputs should not carry REST validation attributes and should have strong nullability/invariants. They represent already-validated state.
- Keep normalization in domain/request-mapping code where canonicalization is required (trim optional text, canonicalize colors, normalize firewall specification).
- Keep semantic/cross-field validation in pure domain validators rather than reimplementing it as superficial Data Annotations. A REST contract may invoke the authoritative shared validator as a non-mutating defense-in-depth check when the exact same signed/domain semantics cross the boundary; that does not move authority out of the daemon/domain layer.
- Move shared maximum lengths and similar invariants to a shared domain limits type, similar to `RuleTemplateLimits`, and make both REST annotations and EF mappings consume the same source of truth. EF entity classes should not define application validation policy.

This should eliminate outcomes such as `InvalidTemplate`/`InvalidGroup`/`InvalidTag` when they represent nothing more than malformed request shape. Domain-specific failure results remain appropriate where structurally valid input cannot be applied.

**Status:** Completed across the W1 boundary migrations and W2.6 audit. Group/tag/template/known-host/network-interface/rule-metadata request shape is declarative on V1 DTOs, mapping performs canonicalization after validation, and semantic firewall/DNS/external-state behavior remains in domain/application code. Shared cross-layer limits live in `Ufw.Shared.Management`; REST annotations, EF mappings, and client controls consume those sources where applicable.

**Primary files:** `Services/KnownHosts/KnownHostService.cs`, `Services/NetworkInterfaces/DaemonNetworkInterfaceSource.cs`, `Services/NetworkInterfaces/NetworkInterfaceInventoryService.cs`, `Services/Rules/RuleGroupService.cs`, `Services/Rules/RuleTagService.cs`, `Services/Rules/RuleTemplateService.cs`, `Services/Rules/RuleMetadataValuesNormalizer.cs`, `Data/Model/KnownHostEntry.cs`, `Data/Model/NetworkInterfaceEntry.cs`, `Data/Model/RuleGroupEntry.cs`, `Data/Model/RuleTagEntry.cs`, `Data/Model/RuleMetadataEntry.cs`; adjacent request/domain contracts in `Ufw.Web.Model`

### KZ-24 [P1-quality] Audit REST DTO validation and eliminate transport-shape checks from application logic

**Problem:** KZ-05 establishes where request validation belongs, and the W1.1 group/tag/template migrations have applied that rule opportunistically, but there is no dedicated whole-API pass proving that every versioned request DTO declares all transport-shape constraints that ASP.NET Core can enforce before controller/application logic runs. The remaining mixed state makes it easy for manual length/null/format checks to survive indefinitely in services/normalizers or for new DTOs to regress to inline validation.

**Audit-start evidence:** `UpdateRuleMetadataRequest` had no validation attributes while `RuleMetadataValuesNormalizer.TryNormalize` rejected null/oversized tag collections, empty GUIDs/group IDs, and oversized notes. Rule-metadata cleanup duplicated selection checks in both controller and service. Group/tag/template DTOs had inconsistent string-validation semantics relative to their request mapping and persistence limits. Known-host DNS/literal source configuration was still classified inside the application workflow. The retained Web-only IPv4/port attributes had no production DTO consumer.

**Refactor target:** Perform a holistic request-contract audit rather than continuing to fix DTO validation only while touching adjacent features:

- Inventory every public/versioned REST request DTO and every manual `BadRequest`/`TryNormalize`/length/null/simple-format guard in Web application code. Classify each guard as transport shape, normalization/canonicalization, cross-field request consistency, or true domain semantics.
- Express transport-shape constraints through Data Annotations or another centralized ASP.NET Core model-validation mechanism on `Ufw.Web.Model` contracts. Move/rework reusable custom validation attributes into a contract-consumable layer where appropriate; do not create a Web.Model -> Web dependency.
- Use shared limits from `Ufw.Shared` whenever the constraint is a cross-layer invariant. Keep genuinely persistence-only constraints adjacent to the ORM model.
- Let `[ApiController]`/MVC model validation reject malformed request shape before controller/application/DAL work. Remove duplicate inline checks once equivalent declarative validation is covered by regression tests.
- Validate raw transport strings as received; validation must not trim or otherwise normalize a value to make it acceptable. Keep normalization/canonicalization explicit only after model validation, and keep semantic/cross-field firewall behavior in pure domain validators unless a constraint is specifically a REST request-shape invariant.
- Add focused DTO/model-validation tests plus representative controller/integration coverage so future request contracts cannot silently bypass the centralized validation path.

**W2.3 progress:** Known hosts now consume shared limits and raw-value DTO validation for metadata, removing the original KZ-14 business-logic shape check. This is an incremental consumer of the target validation architecture, not the holistic KZ-24 closure; DNS/address-source combinations and the rest of the V1 request surface still need the dedicated audit.

**W2.4 progress:** Network-interface name/comment limits now live in `Ufw.Shared.Management.NetworkInterfaces`; daemon response validation, EF mappings, the comment request DTO, and the current client editor consume those shared limits. The service no longer performs its former manual comment-length check.

**W2.6 status:** Completed. `UpdateRuleMetadataRequest` and both cleanup requests now declare collection/null/GUID/length constraints; rule-metadata services consume already-validated input and the dedicated `RuleMetadataValuesNormalizer` is removed. String constraints are evaluated against the raw transport value before any mapping normalization; standard `StringLength`/`RegularExpression` validation therefore rejects values that only become valid after trimming, including padded exact-format tag colors. Known-host request validation now owns literal address validity and the `AddressSource`/`DnsAddressFamily` cross-field contract, eliminating the temporary address/configuration mutation outcomes. Signed firewall mutations now use dedicated REST request DTOs rather than exposing the daemon IPC request models directly. Their model validation inspects the exact raw signed envelope and an operation-specific deserialized copy of the canonical payload, rejects malformed/unknown/duplicate/non-canonical/state-independent invalid values, and then maps to the IPC contract by copying the original signed values and `JsonElement` unchanged. Daemon signature/trust/freshness/replay/current-state validation remains authoritative and unchanged. The retained Web-only `ValidIPv4AddressOrAnyAttribute`/`ValidPortRangeAttribute` were removed: the signed REST boundary reuses `RuleSpecificationValidator` plus canonicality checks instead of introducing partial Web-only firewall semantics. DNS resolution, dependency races, daemon-response integrity, and state-dependent signed-operation preconditions remain outside REST model validation.

**Sequencing:** Schedule this as a dedicated W2 closure pass after the remaining request-owning vertical slices (network interfaces, auth, and any residual rule-metadata request handling) have migrated to their final boundaries, but **before KZ-09 public-error standardization**. At that point the DTO set and shared limits are stable enough for a holistic audit, and KZ-09 can standardize the final validation `ProblemDetails` behavior rather than targeting a mixture of MVC/model-state failures and hand-built bad requests. KZ-24 closes the API-wide completeness aspect of KZ-05; it does not replace KZ-05's W1 boundary rule.

**Primary files:** `Ufw.Web.Model/V1/**/*Request*.cs`, `Ufw.Web.Model/Validation`, `Ufw.Web/Data/Validation`, remaining request normalizers/services/controllers in `Ufw.Web`, and adjacent validation/controller integration tests

### KZ-04 [P2] Decompose `Startup` into feature registration and startup lifecycle units

**Problem:** `Startup` is a 252-line composition root that owns database registration, transaction setup, Identity, JWT validation, bootstrap, every feature registration, CORS, antiforgery, API versioning, Swagger, IPC client construction, HTTP pipeline setup, schema migration, and bootstrap transaction execution. IPC options are also bound/validated twice.

**Evidence:** `Startup.cs:33-198` is all service configuration; `:201-251` configures middleware and performs migration/bootstrap. IPC options are registered with `AddOptions` at `:170-173`, then independently rebound and revalidated at `:175-180`.

**Refactor target:** Keep `Startup` as orchestration only. Extract feature extension methods such as `AddPersistence`, `AddAuthenticationSubsystem`, `AddDaemonClient`, and `AddRuleManagement`. Move migration/bootstrap execution to an explicit startup initializer or deployment migrator. Configure the daemon client from validated options instead of binding the same section twice.

**Primary files:** `Startup.cs`, `Program.cs`, `Configuration/IpcClientOptions.cs`

### KZ-06 [P2] Unify rule metadata/template tag and group dependency handling

**Problem:** Metadata and templates independently resolve tag IDs and an optional group, project tags/groups to shared client/server models, and maintain many-to-many tag relationships. The implementations have already diverged: template updates diff tag relations, while metadata updates delete every relation and reinsert all tags.

**Evidence:** `RuleMetadataRepository.cs:68-107` resolves dependencies and wholesale replaces tags. `RuleTemplateRepository.cs:112-146` has `SynchronizeTags` plus a second dependency resolver. Projection logic is duplicated around `RuleMetadataRepository.cs:256-267` and `RuleTemplateRepository.cs:159-182`.

**Refactor target:** Extract a persistence-level metadata dependency resolver and reusable tag-relation synchronizer. Reuse one tag/group domain projection. Preserve explicit repository methods rather than introducing a deep generic repository hierarchy.

**W2.1 status:** Completed. `RuleManagementDependencyResolver` resolves stable tag/group identities for both live-rule metadata and templates and returns the existing payload-bearing `RuleTagsNotFoundError`/`RuleGroupNotFoundError` variants. `RuleTagRelationSynchronizer` performs one relation diff algorithm for both `RuleMetadataTagEntry` and `RuleTemplateTagEntry`; metadata no longer deletes and reinserts unchanged tag relations. Both read paths project directly into the same `Ufw.Shared.Management.Rules` tag/group models. The small inline constructors remain inside EF projections because introducing expression-splicing infrastructure solely to share those constructors would make SQL translation and review less transparent.

**Primary files:** `Data/Access/Rules/RuleManagementDependencyResolver.cs`, `Data/Access/Rules/RuleTagRelationSynchronizer.cs`, `Data/Access/Rules/Metadata/RuleMetadataDataAccess.cs`, `Data/Access/Rules/Templates/RuleTemplateDataAccess.cs`

### KZ-22 [P2] Standardize mutation/error propagation instead of feature-local outcome plumbing for common failures

**Problem:** The group/tag/template paths define parallel `*MutationOutcome` enums and `*MutationResult` records, then manually translate database exceptions and common conditions into those local concepts before controllers manually translate them again into HTTP responses. Some outcomes are genuinely feature-specific, but common persistence failures such as unique/FK violations are repeatedly reclassified per repository. This creates a chain of near-identical error vocabularies rather than a reusable application error model.

**Evidence:** `RuleGroupRepository.cs:20-100` and `RuleTagRepository.cs:20-99` independently map uniqueness and foreign-key `DbUpdateException`s into `NameConflict`/`InUse`; their controllers then repeat almost identical outcome-to-`ProblemDetails` switches. `RuleTemplateRepository` adds another local `RuleTemplateMutationOutcome` vocabulary for not-found/dependency failures. `Data/Extensions/DbUpdateExceptionExtensions.cs` already provides centralized PostgreSQL constraint classification, but propagation above that classifier remains feature-local.

**Refactor target:** Define a small reusable application/persistence error/result abstraction for common classes of failure, and translate `DbUpdateException` into it at a single persistence boundary. Preserve domain-specific error detail rather than flattening everything: for example, a rule group being in use or a referenced tag not existing may still carry typed domain context, while generic unique/FK/concurrency/storage failures should not require a new enum member and mapping pipeline for every feature. KZ-09 should then perform the final standard application-error-to-ProblemDetails mapping at the HTTP boundary.

Avoid an untyped exception-driven API or one giant catch-all enum. The goal is standard error categories plus optional typed domain details, not loss of semantic information.

**W1.1 status:** The group/tag/template slices now use shared `DataMutationResult` plus typed errors. Common not-found, unique-conflict, and reference-conflict cases remain payloadless markers; template dependency lookup adds payload-bearing `RuleTagsNotFoundError`/`RuleGroupNotFoundError` variants so the error hierarchy preserves exact domain context rather than acting as an enum in disguise. PostgreSQL unique/FK exceptions are translated through `DbUpdateExceptionExtensions`; operation-specific catch filters avoid consuming unrelated storage failures. Other Web slices still carry their legacy outcome vocabularies and migrate later, so KZ-22 remains open globally.

**W2.1 status:** Rule metadata persistence now uses the same error family and a payload-bearing `DataMutationResult<RuleMetadataItem?>`; successful clears deliberately carry `null`, while successful upserts carry the resulting shared domain item. Missing tag/group dependencies retain their typed payloads, and a concurrent FK/reference failure remains a generic `DataMutationReferenceConflictError`. The higher-level metadata workflow still exposes its existing endpoint-oriented outcome for now, so KZ-22 remains open until the remaining vertical slices/public error boundary are migrated.

**Primary files:** `Data/Extensions/DbUpdateExceptionExtensions.cs`, `Services/Rules/RuleGroupMutationOutcome.cs`, `Services/Rules/RuleGroupMutationResult.cs`, `Services/Rules/RuleTagMutationOutcome.cs`, `Services/Rules/RuleTagMutationResult.cs`, `Services/Rules/RuleTemplateMutationOutcome.cs`, `Services/Rules/RuleTemplateMutationResult.cs`, `Services/Rules/RuleGroupRepository.cs`, `Services/Rules/RuleTagRepository.cs`, `Services/Rules/RuleTemplateRepository.cs`, `Api/V1/Controllers/RuleGroupsController.cs`, `Api/V1/Controllers/RuleTagsController.cs`, `Api/V1/Controllers/RuleTemplatesController.cs`

### KZ-07 [P2] Reduce rule-group/rule-tag catalog copy-paste without generic-controller overengineering

**Problem:** Rule groups and tags have nearly identical CRUD orchestration, uniqueness prechecks, race-safe constraint handling, in-use checks, mutation-result plumbing, and controller result mapping. Normalized source comparison puts the two repositories at roughly 85% similarity.

**Evidence:** Compare `RuleGroupRepository.cs` and `RuleTagRepository.cs`, plus the two 59-line controller implementations. Both repositories also carry an unused `using Npgsql;`.

**Refactor target:** After KZ-22 standardizes common mutation/error propagation, extract only the remaining small, named helpers for repeated catalog persistence plumbing. Keep type-specific services/controllers explicit; avoid a generic base controller or generic repository that hides domain differences. Let KZ-09 own common ProblemDetails mapping rather than adding another catalog-specific HTTP helper.

**Primary files:** `Api/V1/Controllers/RuleGroupsController.cs`, `Api/V1/Controllers/RuleTagsController.cs`, `Services/Rules/RuleGroupRepository.cs`, `Services/Rules/RuleTagRepository.cs`, `Services/Rules/RuleGroupService.cs`, `Services/Rules/RuleTagService.cs`

### KZ-08 [P2] Centralize semantic rule-ID extraction from daemon snapshots

**Problem:** The same `RuleId` filtering, null/whitespace removal, distinctness, and `StringComparer.Ordinal` semantics are implemented multiple times with slightly different LINQ forms.

**Evidence:** `RuleInventoryService.cs:12-16`, `RuleMetadataReconciliationService.cs:42-46`, `RuleMetadataService.cs:117-129`.

**Refactor target:** Introduce a tiny domain helper/value set for live semantic identities and reuse it everywhere. It should own validity filtering, ordinal equality, set conversion, and deterministic ordering when needed.

**Status:** Completed in W2.2. `LiveRuleIdentitySet` now owns extraction from authoritative daemon snapshots, filters null/whitespace identities, applies ordinal distinctness/membership, and enumerates deterministically. Inventory enrichment, metadata liveness/reconciliation, batch-delete cleanup, and replacement reconciliation all consume the same Web-domain identity semantics. The helper remains Web-local rather than forcing snapshot-consumption behavior into the shared daemon/client domain.

**Primary files:** `Services/Rules/LiveRuleIdentitySet.cs`, `Services/Rules/RuleInventoryService.cs`, `Services/Rules/RuleMetadataReconciliationService.cs`, `Services/Rules/RuleMetadataService.cs`, `Services/Rules/RuleDaemonGateway.cs`

### KZ-09 [P2] Standardize HTTP error shape and declared response contracts

**Problem:** Errors are returned as a mix of `ProblemDetails`, `ValidationProblemDetails`, anonymous `{ message }` objects, empty 400/404 responses, and an empty antiforgery 400. Several rule mutation endpoints can produce a daemon-mapped 502 but do not declare it in their API contract.

**Evidence:** Anonymous rule errors at `RulesController.cs:41-43,58,77,96,122,141,161`; antiforgery empty 400 at `AntiforgeryValidationMiddleware.cs:19-23`; daemon error mapping can return 502 at `DaemonApiErrorMapper.cs:27-28`.

**Refactor target:** Adopt one ProblemDetails-based public error policy, preferably through centralized exception/problem handling. Add response metadata/conventions so Swagger reflects gateway failures consistently. Validation failures use an ordered structured collection rather than `ValidationProblemDetails.Errors`, allowing repeated property failures and stable validation identities to survive unchanged.

**W2.7.1 status:** The validation half is complete. `Ufw.Web.Model` now defines the browser-visible ProblemDetails/validation DTOs; automatic MVC model-state failures, password-change validation, and daemon validation all emit the same `validationErrors` extension, and the client preserves those structured entries on `ApiRequestException`. Remaining work for W2.7.2 is to migrate anonymous `{ message }` payloads and empty application-generated errors to ProblemDetails and reconcile the endpoint/Swagger response declarations.

**W2.7.2a status:** Runtime producer migration is complete. Application-owned 400/404/conflict failures now use `ApiProblemDetailsFactory`, antiforgery writes ProblemDetails through the registered ASP.NET Core problem-details service, and clear authentication/authorization 401/403 responses intentionally remain bodyless. At this review point, response metadata/Swagger reconciliation remained for W2.7.2b, including accurate representation of statuses that can carry either a typed transaction outcome or ProblemDetails.

**W2.7.2b status:** Completed. Non-success application/daemon response declarations now name `ApiProblemDetails` explicitly, daemon-backed endpoints declare reachable 500/502 failures, and signed mutations declare the remaining daemon application statuses that can cross the gateway. Swagger normalizes bodyless framework failures to no content and advertises ProblemDetails as `application/problem+json`; where a status can instead be a typed transaction result, the generated response exposes both the typed `application/json` body and ProblemDetails media-type alternative. Signed-mutation 403 responses additionally document that framework authorization may be bodyless while application/intent rejection uses ProblemDetails. This closes KZ-09; the temporary legacy client `{ message }` fallback remains separately tracked under CLIENT KZ-11 for post-W acceptance cleanup.

**Primary files:** `Api/V1/Controllers/*.api.cs`, `Api/V1/Controllers/RulesController.cs`, `Api/V1/Controllers/RuleMetadataController.cs`, `Security/AntiforgeryValidationMiddleware.cs`, `Api/V1/Errors/ApiProblemDetailsFactory.cs`, `Api/V1/Errors/DaemonApiErrorMapper.cs`, `Configuration/Swagger/ResponseContractOperationFilter.cs`, `Ufw.Web.Model/V1/Errors/*`

### KZ-10 [P2] Make authentication transaction ownership explicit and reduce service contracts tied to `IdentityUser`

**Problem:** `AuthenticationFlowService` opened transaction scopes and called `RefreshTokenService`, which independently opened transaction scopes. Correctness therefore depended on implicit/ambient Wkg transaction joining semantics. Several workflow contracts also exposed `IdentityUser`/`IdentityResult`, binding orchestration to ASP.NET Identity implementation types.

**Historical evidence:** The pre-W2.5 `AuthenticationFlowService` owned outer transaction scopes while `RefreshTokenService` opened nested scopes for issue/rotation/revocation; `RefreshTokenRotationResult` carried an `IdentityUser` directly.

**Refactor target:** Choose one transaction owner per auth use case and make nested persistence participate explicitly in that context. If Identity is intentionally permanent, keep the implementation coupling local to the auth implementation; otherwise return a small authenticated-user projection instead of `IdentityUser`.

**Status:** Completed in W2.5. `AuthenticationFlowService` is the sole transaction owner for login, refresh, password change, and logout. `RefreshTokenDataAccess` receives that exact `ApplicationDbContext` explicitly and never opens a transaction of its own. Rotation returns only user ID plus replacement-token facts, while the workflow resolves `IdentityUser` only when Identity behavior is required. Password-change results no longer expose `IdentityResult`; Identity validation errors are translated into auth workflow validation fields before leaving the implementation boundary. Tests cover caller-owned rollback, committed failed-login state, replay-family invalidation, and rollback of an Identity password change when a later token-issuance step fails.

**Primary files:** `Services/Auth/AuthenticationFlowService.cs`, `Data/Access/Auth/IRefreshTokenDataAccess.cs`, `Data/Access/Auth/RefreshTokenDataAccess.cs`, `Data/Access/Auth/RefreshTokenRotationResult.cs`, `Services/Auth/PasswordChangeResult.cs`

### KZ-11 [P2] Shrink accidental public surface area

**Problem:** Many application-only interfaces and result records are public despite being consumed only inside `Ufw.Web` and its friend test assemblies. `.editorconfig` makes CA1515 silent project-wide, reducing pressure to keep the assembly surface intentional.

**Evidence:** The project already grants tests `InternalsVisibleTo`, so testability does not require these types to be public.

**Refactor target:** Internalize application-only contracts/results. Keep controllers and genuinely external framework-facing types public where required. Narrow the CA1515 suppression to intentional exceptions rather than silencing the rule for the entire project.

**Primary files:** `.editorconfig`, `Configuration/RefreshTokenOptions.cs`, `Api/V1/Errors/DaemonApiError.cs`, `Api/V1/Errors/IDaemonApiErrorMapper.cs`, `Services/Auth/IAuthenticationFlowService.cs`, `Services/Auth/IAuthenticationTimingService.cs`, `Services/Auth/IJwtTokenService.cs`, `Services/Auth/AccessToken.cs`, `Services/Auth/AuthenticationTokenResult.cs`, `Services/Auth/PasswordChangeResult.cs`, `Services/Auth/PasswordChangeValidationError.cs`, `Services/Auth/PasswordChangeValidationField.cs` and other application-only contracts

### KZ-13 [P2] Retain user-owned interface metadata across transient disappearance

**Problem:** Reconciliation deletes any cached interface not present in the latest daemon list. Because comment and visibility live on that same row, a transiently absent interface loses application-owned metadata and gets a fresh identity/default metadata if it later reappears.

**Historical evidence:** the pre-W2.4 `NetworkInterfaceInventoryRepository` removed rows absent from the latest daemon list and recreated them with fresh identity/default metadata if they later returned.

**Lifecycle decision:** Missing daemon interfaces are not authoritative deletion of application-owned metadata. Retain the row in a soft-deleted/non-present state. If the same interface name reappears, reactivate the existing row so its public identity, comment, and visibility survive. Normal inventory/rule-authoring responses should expose only present interfaces. Permanently deleting retained stale rows is an explicit user cleanup operation, not a reconciliation side effect. Cleanup must revalidate against the daemon-authoritative interface set and delete only rows that are still absent after that revalidation, so an interface that reappeared after the candidate list was loaded cannot be removed.

**Refactor target:** Implement the retained-presence lifecycle in the final network-interface DAL/reconciliation slice, expose stale interface metadata through a dedicated cleanup/read contract, and provide an explicit permanent-delete operation. Keep the server contract domain-specific rather than forcing stale interfaces into the rule-metadata reconciliation DTO. Client-side presentation and the combined cleanup workflow are deferred to CLIENT KZ-24.

**Primary files:** `Data/Access/NetworkInterfaces/NetworkInterfaceDataAccess.cs`, `Services/NetworkInterfaces/NetworkInterfaceInventoryService.cs`, `Data/Model/NetworkInterfaceEntry.cs`, `Api/V1/Controllers/NetworkInterfacesController.*`, `Ufw.Shared/Management/NetworkInterfaces/*`

**Status:** Completed in W2.4. `NetworkInterfaceEntry.IsPresent` records daemon presence without deleting application metadata; reconciliation revives the existing row on reappearance; normal inventory queries filter to present rows; stale retained metadata is exposed separately; and cleanup refreshes daemon-authoritative names in the same reconciliation/delete operation before physically deleting only selected rows that remain absent. Client presentation remains deferred to CLIENT KZ-24.

### KZ-12 [P3-quick] Remove obsolete signing/stale code and preserve reusable validation primitives

**Problem:** Two custom validation attributes currently have no production references, while the RSA JWT key provider is unreferenced and DI always uses ECDSA. Group/tag repositories also contain stale imports. The validation attributes are nevertheless reusable request-shape primitives needed by the upcoming KZ-05 DTO-validation migration, so deleting them now would only recreate them shortly afterward.

**Evidence:** Repository-wide symbol search finds no production use of `ValidIPv4AddressOrAnyAttribute`, `ValidPortRangeAttribute`, or `RsaJwtSigningKeyProvider`.

**Refactor target:** Remove genuinely obsolete signing/stale code, but retain the IPv4/port validation attributes and their focused tests because request-DTO validation remains an active KZ-05/KZ-24 concern. Revisit their ownership, implementation, and messages during the dedicated DTO-validation audit rather than deleting and recreating them.

**Status:** Completed in W1.1 with narrowed scope. The unused RSA JWT signing-key provider is removed and group/tag repository-specific stale imports disappeared with the DAL migration. The two legacy firewall-format validation attributes were retained until the dedicated KZ-24 audit, then removed in W2.6 after that audit established that shared firewall semantics belong exclusively to `RuleSpecificationValidator` and have no correct Web DTO attribute target.

**Primary files:** `Data/Validation/ValidIPv4AddressOrAnyAttribute.cs`, `Data/Validation/ValidPortRangeAttribute.cs`, `Services/Auth/RsaJwtSigningKeyProvider.cs`, `Services/Rules/RuleGroupRepository.cs`, `Services/Rules/RuleTagRepository.cs`

### KZ-16 [P3] Extract auth cookie policy and Identity error mapping

**Problem:** Refresh-cookie append/delete duplicated security options, and password-change field mapping was coupled to the literal Identity error code `"PasswordMismatch"`; every other Identity error was assumed inline to belong to `NewPassword`.

**Historical evidence:** The pre-W2.5 `AuthController` repeated `Secure`/`HttpOnly`/`SameSite`/path/essential cookie options and grouped `IdentityError` instances directly by their raw Identity error code.

**Refactor target:** Use one refresh-cookie policy/factory for append/delete. Put Identity validation translation behind a dedicated mapper with explicit known mappings and fallback behavior.

**Status:** Completed in W2.5. `RefreshTokenCookiePolicy` is the single source of refresh-cookie security attributes for append/delete. `IdentityPasswordChangeErrorMapper` explicitly maps current-password and known replacement-password Identity codes and preserves the previous replacement-password fallback for provider-specific errors. `AuthController` consumes Identity-independent `PasswordChangeValidationError` values rather than inspecting `IdentityResult` or Identity error-code strings.

**Primary files:** `Api/V1/Controllers/AuthController.cs`, `Services/Auth/RefreshTokenCookiePolicy.cs`, `Services/Auth/IdentityPasswordChangeErrorMapper.cs`, `Services/Auth/PasswordChangeResult.cs`

### KZ-17 [P3] Centralize daemon endpoint paths

**Problem:** Daemon read/probe paths are string literals spread across controllers and source adapters.

**Evidence:** `/api/v1/intent/context`, `/api/v1/status`, `/api/v1/rules`, and `/api/v1/network-interfaces` appear in four different files.

**Refactor target:** Move routes into the daemon gateway/protocol client boundary. This naturally disappears if KZ-02 is implemented well.

**Status:** Completed in W1.2.1. The four unsigned read/probe routes are private constants of their feature gateways. Signed rule-mutation routes are not duplicated in Web because their shared request-message types already own that protocol metadata.

**Primary files:** `Api/V1/Controllers/IntentController.cs`, `Api/V1/Controllers/StatusController.cs`, `Services/Rules/RuleDaemonGateway.cs`, `Services/NetworkInterfaces/NetworkInterfaceDaemonGateway.cs`

### KZ-18 [P3] Tighten bulk persistence operations after the boundary refactor

**Problem:** Bulk metadata cleanup first loads entities and then `RemoveRange`s them. This adds tracking/materialization overhead for operations whose result only needs a count.

**Evidence:** `Data/Access/Rules/Metadata/RuleMetadataDataAccess.cs` still materializes rows before `RemoveRange` in `DeleteForRuleIdsAsync` and `DeleteUnmatchedAsync`.

**Refactor target:** After verifying cascade semantics, use `ExecuteDeleteAsync` or an equivalent set-based delete and return the affected-row count.

**Status:** Completed in W2.2. `DeleteForRuleIdsAsync` and `DeleteUnmatchedAsync` now use `ExecuteDeleteAsync` against the final metadata DAL query and return the database-reported affected metadata-row count without materializing/tracking deletion worksets. Regression coverage verifies that database cascade deletion removes `RuleMetadataTagEntry` relations while preserving reusable tag rows.

**Primary files:** `Data/Access/Rules/Metadata/RuleMetadataDataAccess.cs`

### KZ-19 [P3-release-risk] Treat the description-length migration as explicitly destructive history

**Problem:** The migration truncates every template description longer than 512 characters before shrinking the column. `Down` can widen the column but cannot restore discarded text.

**Evidence:** `RuleTemplateDescriptionLength.cs:13-17` performs `LEFT("Description", 512)`.

**Refactor target:** Do not edit an already-applied migration. If this migration has not shipped, decide whether truncation is acceptable; otherwise fail/preflight instead. If it has shipped, document the irreversible behavior and keep future destructive migrations gated by explicit data-migration policy.

**Primary files:** `Data/Migrations/20260930110937_RuleTemplateDescriptionLength.cs`

### KZ-20 [P3-optional] Reduce repeated versioned-controller policy attributes only if conventions stay obvious

**Problem:** Most secured V1 controller contract partials repeat `[Authorize]`, `[ApiController]`, `[ApiVersion(1.0)]`, and no-store response-cache policy.

**Evidence:** Repeated across the V1 `*.api.cs` files; auth intentionally differs because some endpoints are anonymous.

**Refactor target:** Prefer an MVC convention or a very small common attribute/base contract only if it remains transparent in Swagger and authorization review. This is low-value compared with the boundary work and should not drive a generic-controller hierarchy.

**Primary files:** `Api/V1/Controllers/IntentController.api.cs`, `Api/V1/Controllers/AuthController.api.cs`, `Api/V1/Controllers/RuleTagsController.api.cs`, `Api/V1/Controllers/RuleGroupsController.api.cs`, `Api/V1/Controllers/RuleTemplatesController.api.cs`, `Api/V1/Controllers/RulesController.api.cs`, `Api/V1/Controllers/RuleMetadataController.api.cs`, `Api/V1/Controllers/StatusController.api.cs`, `Api/V1/Controllers/KnownHostsController.api.cs`, `Api/V1/Controllers/NetworkInterfacesController.api.cs`

### KZ-21 [P3-quick/perf] Prefer `ToListAsync` over `ToArrayAsync` for EF materialization when array identity is irrelevant

**Problem:** `ToArrayAsync` in EF Core materializes through an intermediate growable collection and then produces an array, so using it when downstream code only needs an enumerable/read-only collection introduces a needless final allocation and copy. After the W1.1 catalog/template migrations, W2.1 metadata read migration, W2.2 set-based metadata cleanup, and W2.4 network-interface migration, 3 `ToArrayAsync` call sites remain in `Ufw.Web`.

**Evidence:** Current uses remain in known hosts, the rule metadata replacement workset, and rule-group inventory. The W1.1 template inventory read, W2.1 metadata read, and W2.4 network-interface read paths project their final shared read models with `ToListAsync`; W2.2 removed both bulk-cleanup materializations by moving those deletes to `ExecuteDeleteAsync`; shared tag/group dependency resolution likewise uses `ToListAsync`. The surviving array uses still need the planned audit because some are mutation/entity worksets where an array may be locally reasonable while others only enumerate/project the result.

**Refactor target:** Audit the surviving sites and use `ToListAsync` wherever the exact concrete collection type is immaterial. Retain `ToArrayAsync` only where an array is deliberately part of the local/API/domain contract or array semantics materially simplify subsequent work. Where KZ-01 removes whole-inventory re-queries entirely, delete the materialization rather than mechanically changing it.

**Primary files:** `Data/Access/KnownHosts/KnownHostDataAccess.cs`, `Data/Access/Rules/Metadata/RuleMetadataDataAccess.cs`, `Data/Access/Rules/Groups/RuleGroupDataAccess.cs`

**Status:** Completed in W2.8. All three surviving sites were audited after the W2 query-shape migrations. Rule-group inventory now projects its final read model in SQL rather than materializing entity graphs for a second client-side projection. Known-host inventory keeps only its address normalization/persisted-data validation as EF's final client projection while SQL selects the required columns. Rule-metadata replacement needs a tracked mutable workset but no array semantics. All three use `ToListAsync`, and no production `ToArrayAsync` remains in `Ufw.Web`.

## Recommended blitz sequence

1. **Safe deletions, correctness, and mechanical allocation cleanup:** KZ-12, KZ-14, KZ-15, KZ-21. These reduce noise before structural work and are easy to verify.
2. **Lock the model/DAL rules before moving code:** KZ-01, KZ-05, KZ-23. Establish persistence-entity vs shared-domain vs transport-contract roles, request mapping, the domain-sliced DAL boundary, and the rule that orchestration layers must earn their existence.
3. **Normalize daemon/application boundaries:** KZ-02 and KZ-03. Establish the daemon gateway and move signed-protocol interpretation out of metadata persistence orchestration.
4. **Repair mutation/error and relationship plumbing:** KZ-22, then KZ-06 and KZ-08. Standardize persistence/application failures, consolidate tag/group relationship handling, and prevent semantic-rule-ID logic from drifting.
5. **Collapse repetitive feature plumbing and close request validation:** KZ-07, then KZ-24 after the request-owning vertical slices have stabilized. Run the holistic DTO-validation audit before KZ-09 so the public error contract targets one final model-validation path.
6. **Standardize public HTTP errors:** KZ-09 after KZ-24, KZ-22, and the daemon gateway are stable.
7. **Composition and final surface cleanup:** auth KZ-10/KZ-16 and daemon-route KZ-17 are complete. Defer KZ-04/KZ-11 until registrations and public contracts stabilize, then close migration policy KZ-19 and optional declaration cleanup KZ-20.
8. **Behavior/performance decisions:** KZ-13, KZ-18, KZ-19, and optionally KZ-20.

## Refactoring guardrails

- Do not create a mapping type merely because data crossed a layer boundary. Introduce a new type when semantics, trust level, lifetime, or ownership actually changes.
- Never expose EF persistence entities or `IQueryable` outside the DAL. Shared domain/read models are explicitly allowed to cross DAL -> API -> client when they are stable data concepts.
- Do not pass versioned HTTP request DTOs into the DAL. Validate them at the REST boundary, then pass normalized typed arguments or a meaningful internal command/query model.
- Do not require `Controller -> ApplicationService -> DAL` mechanically. Keep a coordinator only when it owns real multi-step, cross-source, transactional, or reusable workflow behavior.
- Controllers may compose response envelopes/sidecars, but they must not join or filter separately materialized database datasets to reconstruct a query that SQL should have answered.
- Do not replace explicit domain-sliced DAL components/controllers with a generic CRUD framework. The duplication is real, but the group/tag/template semantics already diverge in meaningful ways.
- Keep daemon protocol validation strict. The target is to relocate it behind a boundary, not weaken it.
- Keep database unique/FK exception handling even when prechecks remain; the database catch is what makes concurrent writes race-safe. Standardize how those exceptions propagate rather than removing the catches.
- Do not move semantic or cross-field firewall validation into Data Annotations merely to make validation look uniform. Annotations own REST/request shape; domain validators own domain semantics.
- Do not replace feature-specific outcomes with one untyped catch-all. Standardize common error categories while preserving typed domain context where clients or application logic need it.
- Do not edit already-applied EF migrations to “clean them up.” Refactor model configuration and add new migrations when schema changes are required.
- Preserve the intentional best-effort nature of post-firewall metadata cleanup, but give it an explicit cancellation/error policy instead of broad catch-and-swallow behavior.

## Additional observations that do not warrant standalone work items

- `ApplicationDbContext`, model mapping classes, JWT ECDSA key loading, DNS deterministic address selection, the low-level DB exception classifier itself, and the antiforgery marker are straightforward and do not need architectural rewrites. KZ-22 targets the repeated propagation/mapping above the existing DB classifier.
- The existing client/server sharing through `Ufw.Web.Model` is a strength, not a layering defect. The kaizen should clarify which of those types are shared domain/read models versus versioned request/response envelopes instead of inserting redundant server-only DTOs.
- The EF mapping files contain repetitive fluent configuration by nature. Abstracting that boilerplate would likely make the schema harder to audit.
- Public API contract/implementation split via `*.api.cs` partial controllers is coherent. The issue is repeated cross-cutting attributes/error declarations, not the split itself.
- The default settings file contains development-style credentials/passwords, but the project explicitly excludes it from build/publish output. Treat it as a template/documentation concern rather than an application-code defect.

## Definition of done for the blitz

- No production references remain to the deleted RSA provider; retained validation attributes are either consumed by the KZ-05 request-validation boundary or removed if they prove unnecessary there.
- Known-host invalid metadata and daemon multi-error validation have regression tests.
- Controllers no longer inject `IUfwClient` directly for normal daemon workflows.
- Daemon transport exceptions are converted to the public HTTP error contract in one place.
- Rule metadata replacement reconciliation receives domain facts rather than parsing signed IPC JSON itself.
- DAL methods return shared domain/read models, mutation facts, or standardized errors rather than endpoint response envelopes; EF persistence entities never escape the DAL.
- Successful DAL writes do not rebuild whole inventories inside the write transaction; endpoints that still return a refreshed inventory perform the normal read query after commit.
- Direct `ApplicationDbContext`/`DbSet`/EF query usage is confined to the data-access layer (apart from composition/migrations), and `IQueryable` does not cross that boundary.
- Representative data-heavy endpoints have bounded SQL-command-count regression coverage so N+1/query fan-out cannot reappear unnoticed.
- Ceremonial one-hop feature services are removed; retained coordinators have documented multi-step/cross-source responsibilities.
- REST request-shape validation (required values, lengths, simple formats/nullability) is declarative; versioned request DTOs are mapped to non-null/normalized domain arguments before DAL/domain use; domain/cross-field validation remains in pure domain validators.
- Shared limits and semantic rule-ID handling have one source of truth, consumed consistently by API validation and persistence mapping.
- Common persistence/application failure categories use a reusable propagation model; feature-local mutation enums/results exist only where they add real domain semantics.
- EF query materialization uses `ToListAsync` by default when no array contract is required; remaining `ToArrayAsync` uses are deliberate.
- Metadata and template tag/group relationship logic use the same resolver/synchronizer primitives.
- Cancellation behavior for best-effort metadata cleanup is explicit and tested.
- Internal application contracts are actually `internal` unless there is a documented reason for public visibility.
- The intended retention semantics for temporarily missing network interfaces are documented and covered by tests.
- No already-applied migration is rewritten as part of cleanup.
