# IPC test adapter architecture

## Purpose

`Ufw.Ipc.Tests` provides an in-process test environment for the IPC client, daemon request pipeline, and daemon routing stack. It lets protocol tests exercise the production path down to byte-stream framing without starting separate `Ufw.Web` and `Ufw.Systemd` processes or binding a platform-specific IPC endpoint.

The adapter replaces environment-specific hosting concerns, not protocol behavior. The default host uses the production client, application serializer, ITP implementation, daemon network application and worker, request middleware, routing tree, and response handlers. Test-specific components can be substituted through dependency injection when a scenario intentionally needs different behavior or fault injection.

## Per-test host

Every `RunAsync` invocation creates an isolated host with independent client and server MEDI containers, an in-process transport broker, and a test endpoint map. No host state is shared between test cases.

The server side runs the production `NetworkApplication`. Its normal `NetworkApplicationWorker` instances accept connections from the in-process transport rather than the OS transport configured for a deployed daemon. Once a connection is accepted, request processing follows the same production path:

1. obtain the transport stream,
2. apply the configured transport-security service,
3. read one ITP frame,
4. decode the application document from the frame payload,
5. execute the daemon request/response pipeline,
6. route the request to the matched endpoint,
7. encode the response document and write it as an ITP frame.

The client side uses the production `UfwClient`, response handlers, application serializer, ITP implementation, and transport-security abstractions. Its transport service connects to the same per-host broker.

This composition means protocol, middleware, or routing regressions remain visible to adapter-driven tests. A test-specific server worker is not part of the default path.

## In-process transport

The transport broker pairs each client connection with a server connection backed by two `System.IO.Pipelines` pipes, one for each direction. Each endpoint sees a normal full-duplex `Stream` through the production `ITransportLayerConnection` abstraction.

The stream preserves the semantics relevant to protocol testing: reads can wait for future writes, reads may return partial data, the two directions operate independently, and disposal is visible to the peer as completion. Because the transport is process-local, tests do not depend on named pipes, Unix sockets, fixed TCP ports, filesystem state, elevated privileges, or platform-specific IPC support.

A broker belongs to exactly one test host, so parallel tests cannot consume each other's connections.

## Routing and test endpoints

Tests can register routing targets programmatically without defining a daemon controller. `ITestEndpointMapBuilder` creates normal `ApiEndpointMapping<IRequestMessage, IResponseMessage>` mappings through the same endpoint-mapping factory used by daemon routing. `TestApiEndpointMap` supplies those mappings to the production routing tree consumed by `EndpointInvocationMiddleware`.

The test endpoint map changes the set of available endpoints, not the routing algorithm. Method matching, route matching, middleware ordering, request deserialization, endpoint scoping, and response serialization therefore remain production behavior.

Endpoint handlers receive the endpoint's scoped `IServiceProvider`, so test services participate in the same request-scope boundary as daemon controllers.

## Serialization

`JsonMessageSerializer` is used on both sides of the in-process connection. It operates only on complete application-document bytes and depends on the shared `AotJsonSerializerContext` abstraction so production can use `MessageJsonSerializerContext` while tests use `HybridMessageJsonSerializerContext`. ITP stream framing is composed separately by the production client/daemon and by the adapter's raw exchange helpers.

The hybrid context resolves known IPC contracts from the production source-generated context first. A reflection-based fallback allows tests to define small request and response DTOs without extending the production source-generation set merely for test data. Tests that verify an established production wire contract should use the production DTOs registered by `MessageJsonSerializerContext`.

## Facade and extension model

`IpcProtocolTestBase` is the MSTest-facing facade. Derived test classes configure reusable class-level defaults and execute scenarios through higher-order `RunAsync` or typed `SendAsync` helpers. The lambda receives an `IIpcTestContext` for the current host.

Configuration is layered for each run in this order:

1. adapter defaults,
2. class-level async configuration hooks,
3. per-run synchronous configuration,
4. per-run async configuration.

Server services, client services, endpoints, and host options all follow this model. Each run builds new providers after configuration has completed, so service replacement is local to that run.

`IIpcTestContext` exposes two levels of interaction:

- typed requests through the production `IUfwClient`, and
- raw stream/message operations for framing, malformed-input, fragmentation, peer-lifecycle, and similar protocol scenarios.

`ProcessPipelineAsync` is available when a test intentionally wants to isolate middleware/routing behavior from the transport. It is not the default path for protocol tests.

### Worker failure boundary

`NetworkApplicationWorker` treats connection-scoped transport, TLS-authentication, and malformed-protocol failures as failures of the current peer. The connection is disposed, the failure is logged, and the worker returns to accepting requests. This keeps malformed or disconnected peers from permanently consuming daemon serving capacity.

The worker does not catch arbitrary exceptions. Endpoint/controller exceptions are converted into protocol-level error responses by the endpoint mapping layer; unexpected exceptions outside the known peer/connection boundary remain visible and fault the worker/application rather than being silently swallowed. Adapter-driven tests therefore exercise the same failure boundary as production.

## Cancellation and lifetime

The base facade links three possible cancellation sources into each run:

- `TestContext.CancellationToken`, supplied by MSTest,
- an explicit token supplied by the caller,
- the optional adapter-level `IpcTestOptions.TestTimeout`.

The linked token flows through host startup, transport accepts/connects, stream operations, serialization, routing, endpoint execution, arrange hooks, and the test lambda.

Host disposal cancels the daemon application and awaits its completion before disposing the client scope, client and server service providers, and transport broker. Cleanup does not silently abandon daemon tasks after a timeout. Worker or disposal failures are surfaced after all owned cleanup operations have been attempted.

The `IIpcTestContext` and services obtained from it are valid only for the enclosing `RunAsync` invocation. Raw streams returned by `ConnectRawAsync` are caller-owned and should be disposed within that invocation.

## Production integration boundary

The adapter requires only narrow production seams:

- `JsonMessageSerializer` accepts the common `AotJsonSerializerContext` abstraction so tests can supply alternate metadata resolution while production retains source-generated metadata.
- `Ufw.Systemd` and `Ufw.Ipc.Client` expose internals to `Ufw.Ipc.Tests` where the test composition needs daemon/application or client-handler implementation types.

No test-only transport, endpoint, or worker is selected by deployed application configuration.
