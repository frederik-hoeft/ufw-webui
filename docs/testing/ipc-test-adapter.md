# IPC test adapter architecture

## Purpose

`Ufw.Ipc.Tests` hosts a reusable in-process test adapter for the IPC packet stack and daemon-side request routing. The adapter exercises the real production framing, serialization, middleware pipeline, and routing tree without starting `Ufw.Systemd` or `Ufw.Web` as separate processes and without depending on OS named-pipe or Unix-socket endpoints.

It exists so protocol and routing tests can stay focused on Arrange/Act/Assert while setup, teardown, DI lifetime, and transport plumbing stay deterministic and parallel-safe.

## Boundaries

| In scope | Out of scope |
| --- | --- |
| IPC framing (header/payload newline protocol) | Browser ↔ ASP.NET HTTP |
| `IMessageSerializer` read/write path | Privileged UFW command execution |
| Daemon request middleware pipeline | Mutual TLS / certificate material |
| Routing tree match behavior | Multi-process integration against a live daemon |
| Typed client round-trips via `IUfwClient` | Full ASP.NET controller test host for `Ufw.Web` |
| Programmatic test-only endpoints | Source-generated production controller map as the only option |

The adapter deliberately does **not** replace domain-level tests of UFW interop. It is the substrate those tests can later build on once mutation protocols exist.

## Runtime model

Each `RunAsync` invocation builds an isolated host:

```
┌──────────────────────────── Test process ────────────────────────────┐
│                                                                      │
│  IpcProtocolTestBase.RunAsync(...)                                   │
│           │                                                          │
│           ▼                                                          │
│  IpcTestHost (per-run lifetime)                                      │
│    ├─ Server ServiceProvider (MEDI)                                  │
│    │    ├─ IMessageSerializer (JsonMessageSerializer + hybrid JSON)  │
│    │    ├─ IRequestResponsePipeline + production middleware          │
│    │    ├─ TestApiEndpointMap (programmatic routes)                  │
│    │    ├─ IpcTestServerWorker × N                                   │
│    │    └─ InProcessServerTransportService                           │
│    ├─ Client ServiceProvider (MEDI)                                  │
│    │    ├─ IUfwClient (production UfwClient)                         │
│    │    ├─ response handler pipeline                                 │
│    │    └─ InProcessClientTransportService                           │
│    └─ InProcessTransportBroker                                       │
│         └─ DuplexStreamPair (System.IO.Pipelines, no OS IPC)         │
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
```

Key properties:

- **No OS IPC.** Client and server halves are connected with in-memory duplex pipes. CI containers do not need `/run`, Docker, or elevated privileges.
- **Real protocol stack by default.** Framing uses `JsonMessageSerializer`; daemon handling uses `RequestValidationMiddleware`, `RequestLoggingMiddleware`, `EndpointInvocationMiddleware`, and `RequestResponsePipeline`.
- **Isolated DI.** Server and client each get a fresh `ServiceProvider` per run. Class-level and per-run configuration callbacks layer on top of defaults without sharing mutable container state across tests.
- **Parallel-safe.** `MSTestSettings` enables method-level parallelization. No static mutable host, no shared broker, no process-wide pipe name.
- **Deterministic cleanup.** `IpcTestHost.DisposeAsync` cancels workers, joins them with a bounded wait, then disposes client scope, both containers, and the broker (including any orphaned accepted connections).

## Primary types

| Type | Role |
| --- | --- |
| `IpcProtocolTestBase` | Abstract façade. Override configuration hooks; call `RunAsync` / `SendAsync`. |
| `IIpcTestContext` | Per-run handle given to test lambdas (`Client`, raw exchange, pipeline-only). |
| `IpcTestRunConfiguration` | Optional per-case overrides (endpoints, DI, arrange, options). |
| `IpcTestOptions` | Host tunables (worker count, request timeout, debug mode, test timeout). |
| `ITestEndpointMapBuilder` | Fluent registration of GET/POST/PUT/DELETE handlers without controllers. |
| `TestApiEndpointMap` | `ApiEndpointMap<IMessage,IMessage>` built from the builder; production routing tree. |
| `InProcessTransportBroker` | Matches `ConnectAsync` with `AcceptAsync` via duplex pairs. |
| `HybridMessageJsonSerializerContext` | Production source-gen metadata first, reflection fallback for one-off test DTOs. |
| `IpcTestServerWorker` | Same request loop shape as `NetworkApplicationWorker`, but continues after connection-level failures so malformed-packet tests do not kill the host. |

## Integration points with production code

Targeted, non-breaking production adjustments that enable the adapter:

1. **Friend assemblies** — `Ufw.Ipc.Tests` is visible to internals of `Ufw.Ipc.Shared`, `Ufw.Ipc.Client`, `Ufw.Systemd`, and `Ufw.Web`.
2. **`JsonMessageSerializer` constructor** — accepts `AotJsonSerializerContext` so tests can supply the hybrid context while production still registers `MessageJsonSerializerContext`.
3. **Empty payload drain** — `EmptyJsonPipeMessageBlob.TryReadAsync` / `ReadAsync` succeed as no-ops so validation middleware can drain empty bodies (including pipeline-only tests that never crossed the wire).

Production Jab and client MEDI registrations also expose `AotJsonSerializerContext` alongside `MessageJsonSerializerContext` so constructor injection keeps working.

## Test styles supported

1. **Typed full-stack** — `context.SendAsync` / `context.Client` through real client handlers and server routing.
2. **Raw envelope** — `ExchangeRawAsync` for serializer-level request/response without client handlers.
3. **Malformed bytes** — `ExchangeBytesAsync` for invalid frames; workers stay alive for a follow-up valid request.
4. **Pipeline-only** — `ProcessPipelineAsync` for routing/middleware without transport.
5. **Custom DI / mocks** — replace any server or client service via configure callbacks while keeping defaults for the rest.

## Failure and resource policy

- Host construction failures dispose any partially created providers/broker before rethrowing.
- Worker faults during a connection are logged and the loop continues; teardown still cancels and disposes.
- Test timeouts (`IpcTestOptions.TestTimeout`) cancel the run CTS linked into the host, so blocked IO does not leak workers across tests.
