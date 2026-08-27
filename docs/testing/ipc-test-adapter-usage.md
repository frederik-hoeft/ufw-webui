# IPC test adapter usage

## Project setup

`Ufw.Ipc.Tests` already references the IPC client/shared libraries, the daemon project, Roslyn routing abstractions, and `Microsoft.AspNetCore.App` (for MEDI). Friend-assembly attributes are registered on the production projects so tests can construct internal pipeline types without widening public API surface.

Parallel execution is enabled at method scope in `MSTestSettings.cs`.

## Basic typed test

Subclass `IpcProtocolTestBase`, register endpoints once for the class, and keep the test body as pure act/assert:

```csharp
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Tests.Adapter;
using Ufw.Ipc.Tests.Adapter.Endpoints;

[TestClass]
public sealed class PingTests : IpcProtocolTestBase
{
    protected override ValueTask ConfigureEndpointsAsync(
        ITestEndpointMapBuilder endpoints,
        CancellationToken cancellationToken)
    {
        endpoints.MapGet(
            "/api/v1/ping",
            static _ => ValueTask.FromResult(new OkResponse()));
        return ValueTask.CompletedTask;
    }

    [TestMethod]
    public Task Ping_ReturnsOk() => RunAsync(async (context, cancellationToken) =>
    {
        OkResponse response = await context.SendAsync<OkResponse>(
            RequestMethod.Get,
            "/api/v1/ping",
            cancellationToken);
        Assert.IsNotNull(response);
    }).AsTask();
}
```

`RunAsync` returns `ValueTask`. MSTest discovers `Task`-returning methods, so `.AsTask()` is the usual bridge.

## Per-test endpoints and arrange hooks

Use `IpcTestRunConfiguration` (or the `configureEndpoints` convenience overload) when a case needs its own route table or pre-act setup:

```csharp
[TestMethod]
public Task Echo_RoundTrip() => RunAsync(
    configureEndpoints: static endpoints =>
    {
        endpoints.MapPost<EchoRequest, EchoResponse>(
            "/api/v1/echo",
            static (request, _) => ValueTask.FromResult(new EchoResponse(request.Text)));
    },
    actAsync: async (context, cancellationToken) =>
    {
        EchoResponse response = await context.SendAsync<EchoRequest, EchoResponse>(
            RequestMethod.Post,
            "/api/v1/echo",
            new EchoRequest("hi"),
            cancellationToken);
        Assert.AreEqual("hi", response.Text);
    }).AsTask();
```

Class-level `ConfigureEndpointsAsync` runs first; per-run registrations append afterward on a fresh map builder.

## Custom DI (server and/or client)

Replace production services with test doubles without rebuilding the whole host by hand:

```csharp
[TestMethod]
public Task UsesMockClock() => RunAsync(
    actAsync: async (context, cancellationToken) =>
    {
        // assert against behavior that depends on the replaced service
        await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/ping", cancellationToken);
    },
    configuration: new IpcTestRunConfiguration
    {
        ConfigureServerServices = services =>
        {
            // remove/replace descriptors as needed
            services.AddSingleton<IMyService, FakeMyService>();
        },
        ConfigureClientServices = services =>
        {
            // e.g. swap ITransportSecurityService or a response handler
        },
        ConfigureEndpoints = endpoints =>
        {
            endpoints.MapGet("/api/v1/ping", static _ => ValueTask.FromResult(new OkResponse()));
        },
    }).AsTask();
```

Hooks available on the base class (class-wide) and on `IpcTestRunConfiguration` (per run):

- `ConfigureServerServices` / `ConfigureServerServicesAsync`
- `ConfigureClientServices` / `ConfigureClientServicesAsync`
- `ConfigureEndpoints` / `ConfigureEndpointsAsync`
- `ConfigureOptions` / `ConfigureOptionsAsync`
- `ArrangeAsync` (runs after the host is up, before act)

Async overloads are preferred when work can await; sync actions are still applied in a defined order (class async → per-run sync → per-run async).

## Low-level protocol tests

### Raw envelopes

```csharp
await using IMessage request = await context.MessageSerializer.SerializeAsync(
    id: "/api/v1/ping",
    method: RequestMethod.Get.ToString(),
    payload: (object?)null,
    type: typeof(object),
    cancellationToken);

await using IMessage response = await context.ExchangeRawAsync(request, cancellationToken);
Assert.AreEqual("200", response.Id);
```

### Malformed frames

```csharp
ReadOnlyMemory<byte> garbage = Encoding.UTF8.GetBytes("{not-json\n{}\n");
await Assert.ThrowsAsync<Exception>(async () =>
{
    await using IMessage _ = await context.ExchangeBytesAsync(garbage, cancellationToken);
});

// Host remains usable:
OkResponse ok = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/ping", cancellationToken);
```

### Pipeline-only (no transport)

```csharp
await using IMessage request = await context.MessageSerializer.SerializeAsync(
    "PATCH",
    "/api/v1/x",
    payload: (object?)null,
    typeof(object),
    cancellationToken);

await using IMessage response = await context.ProcessPipelineAsync(request, cancellationToken);
Assert.AreEqual("501", response.Id);
```

## Endpoint mapping API

`ITestEndpointMapBuilder` covers the common verbs and free-form method strings:

| Method | Use |
| --- | --- |
| `MapGet` / `MapDelete` | No request body (or ignored body) |
| `MapPost` / `MapPut` | Typed request body + typed response |
| `Map(method, route, handler)` | Arbitrary method string |
| `Map(ApiEndpointMapping<...>)` | Drop in a pre-built production mapping |

Handlers may take `IServiceProvider` when they need scoped services. Responses must implement `IIdentifiable` (typically by deriving from `OkResponseBase` / `ResponseMessage`).

Routes may be written with or without a leading `/`; the builder normalizes them.

## JSON payloads in tests

Production `MessageJsonSerializerContext` remains the source of truth for known IPC DTOs. The adapter’s hybrid context falls back to reflection for one-off test types (`EchoRequest`, etc.), so you do not need to regenerate STJ contexts for every smoke DTO.

If a test DTO must match production wire shape exactly, prefer types already registered on `MessageJsonSerializerContext`.

## Options

```csharp
protected override ValueTask ConfigureOptionsAsync(IpcTestOptions options, CancellationToken cancellationToken)
{
    options.WorkerCount = 2;
    options.RequestTimeout = TimeSpan.FromSeconds(5);
    options.DebugMode = true;          // 500 responses include exception detail
    options.TestTimeout = TimeSpan.FromSeconds(30);
    return ValueTask.CompletedTask;
}
```

## Cleanup guarantees

Do not keep `IIpcTestContext` beyond the `RunAsync` lambda. The host disposes when the lambda completes (success or fault). Nested raw streams from `ConnectRawAsync` should be disposed inside the lambda (`await using`).

## What not to do

- Do not open real named pipes or Unix sockets from these tests; use the in-process broker.
- Do not share a single host across tests via static fields; isolation is the point.
- Do not register mutating daemon endpoints here that bypass the future signed-intent security boundary—the adapter is for protocol/routing fidelity, not for weakening production authorization design.
