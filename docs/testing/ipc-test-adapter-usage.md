# IPC test adapter usage

## Basic test shape

Derive an MSTest class from `IpcProtocolTestBase`. The base class creates a fresh in-process IPC host for every helper invocation and supplies the test runner's cancellation token automatically.

Register endpoints at class scope when they are shared by the class:

```csharp
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

The facade returns `ValueTask`; `.AsTask()` is convenient for MSTest methods declared with a `Task` return type.

## Per-test routing targets

A test can add its own routing targets without defining a daemon controller:

```csharp
[TestMethod]
public Task Echo_RoundTrips() => RunAsync(
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

These mappings are routed by the normal daemon routing tree and `EndpointInvocationMiddleware`. They are test routing targets, not a separate test router.

## Dependency injection

The adapter creates independent server and client MEDI containers for each run. Override the class-level hooks for defaults used by every test in a class, or use `IpcTestRunConfiguration` for a single scenario.

```csharp
[TestMethod]
public Task UsesTestService() => RunAsync(
    actAsync: async (context, cancellationToken) =>
    {
        OkResponse response = await context.SendAsync<OkResponse>(
            RequestMethod.Get,
            "/api/v1/value",
            cancellationToken);
        Assert.IsNotNull(response);
    },
    configuration: new IpcTestRunConfiguration
    {
        ConfigureServerServices = services =>
        {
            services.AddSingleton<IMyService, TestService>();
        },
        ConfigureEndpoints = endpoints =>
        {
            endpoints.MapGet(
                "/api/v1/value",
                static (services, _) =>
                {
                    _ = services.GetRequiredService<IMyService>();
                    return ValueTask.FromResult(new OkResponse());
                });
        },
    }).AsTask();
```

Available class-level hooks are:

- `ConfigureServerServicesAsync`
- `ConfigureClientServicesAsync`
- `ConfigureEndpointsAsync`
- `ConfigureOptionsAsync`

`IpcTestRunConfiguration` supplies corresponding per-run service and endpoint hooks plus `ArrangeAsync`. Per-run configuration is applied after class-level configuration, so an individual test can replace a class default without changing other tests.

## Typed and raw protocol access

Use the typed helpers for ordinary protocol requests:

```csharp
RuleListResponse response = await context.SendAsync<RuleListResponse>(
    RequestMethod.Get,
    "/api/v1/rules",
    cancellationToken);
```

Use `ExchangeRawAsync` when the request envelope itself is part of the scenario:

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

Use `ConnectRawAsync` or `ExchangeBytesAsync` when the test needs control below the message abstraction, for example to write a frame in fragments, omit framing bytes, close a peer early, or send malformed serialized data.

`ProcessPipelineAsync` bypasses transport and serialization intentionally. Use it only when the subject of the test is the daemon middleware/routing pipeline itself.

## Worker-level protocol failures

The default host runs the production `NetworkApplication` and `NetworkApplicationWorker`, including the production worker failure boundary. Malformed protocol data, connection I/O failures, and TLS-authentication failures terminate the current connection but do not terminate the worker. This makes it possible to verify recovery by sending invalid input and then issuing a normal request through the same host.

Unexpected worker failures are not converted into successful test completion. They surface through the network application task when the host is disposed, so protocol tests continue to detect production lifecycle regressions.

## Cancellation and timeouts

Every `RunAsync` invocation links the current `TestContext.CancellationToken` with any explicit token passed to the helper. The resulting token is passed to configuration hooks, host operations, arrange code, and the test lambda.

`IpcTestOptions.TestTimeout` can provide an additional adapter-level ceiling:

```csharp
protected override ValueTask ConfigureOptionsAsync(
    IpcTestOptions options,
    CancellationToken cancellationToken)
{
    options.TestTimeout = TimeSpan.FromSeconds(30);
    options.RequestTimeout = TimeSpan.FromSeconds(5);
    return ValueTask.CompletedTask;
}
```

The adapter-level timeout complements MSTest cancellation; it does not replace it.

## Lifetime rules

Keep `IIpcTestContext` and services resolved from it inside the `RunAsync` lambda. A host is disposed when the helper completes, including when arrange or test code throws or is canceled.

Raw streams returned by `ConnectRawAsync` are owned by the caller:

```csharp
await using Stream stream = await context.ConnectRawAsync(cancellationToken);
```

The host cancels and awaits the daemon network application before disposing its DI containers and transport. Cleanup failures are surfaced rather than converted into successful test completion.

## Choosing payload types

Known production IPC DTOs use `MessageJsonSerializerContext`, the source-generated production metadata set. The test host adds a reflection fallback for small test-only DTOs so routing tests do not need to modify production serializer metadata merely to introduce fixtures.

When a test is specifically verifying an established wire contract, use the production DTO rather than an equivalent test type.
