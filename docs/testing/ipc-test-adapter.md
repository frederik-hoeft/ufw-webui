# IPC protocol test adapter

The IPC test adapter provides an in-process environment for testing the same client-to-daemon protocol stack used in production without starting separate processes or binding a platform-specific Unix socket or named pipe.

Its purpose is not to replace the production stack with test doubles. The adapter substitutes the physical transport and, where a test requires it, transport security. Framing, application serialization, request binding, middleware, routing, endpoint invocation, response handling, timeouts, and worker lifecycle remain the production implementation. This makes the adapter suitable for both ordinary typed request tests and malformed-wire/failure-path tests.

## Test topology

Every test run owns an isolated client container, daemon container, endpoint map, and in-process full-duplex transport broker. The client and daemon see ordinary streams; the broker connects those streams using independent pipelines in each direction.

The adapter boundary sits below the protocols being tested: test code calls the production IPC client, the client exchanges bytes over an in-process full-duplex stream, and the production daemon network pipeline routes the decoded request to a test or production endpoint. A typed test therefore still traverses ITP framing and the application protocol rather than invoking a controller directly.

No broker or dependency-injection state is shared between test runs, so parallel tests cannot consume one another's connections.

## Choosing the test surface

Use the highest-level surface that still exposes the behavior under test:

| Surface | Use it for |
| --- | --- |
| Typed send helpers | Normal requests, response contracts, routing behavior, and end-to-end protocol integration. |
| Raw application-message exchange | Envelope validation, payload-presence rules, and application documents that cannot be represented by normal request DTOs. |
| Raw application bytes | Malformed or unusual JSON documents inside otherwise valid ITP frames. |
| Raw stream/byte exchange | ITP framing, fragmentation, wrong versions, early disconnects, and transport-error behavior. |
| Pipeline-only processing | Middleware/routing tests that intentionally exclude serialization and transport. |

Transport or serialization should not be bypassed merely to make a test shorter. If the behavior is a wire contract, exercise it through the wire-oriented surface.

## Basic usage

Protocol tests derive from `IpcProtocolTestBase`. Shared endpoints and dependencies can be configured at class scope; a run can then add or replace configuration for one scenario.

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
    public Task Ping_ReturnsOkAsync() => RunAsync(async (context, cancellationToken) =>
    {
        OkResponse response = await context.SendAsync<OkResponse>(
            RequestMethod.Get,
            "/api/v1/ping",
            cancellationToken);

        Assert.IsNotNull(response);
    }).AsTask();
}
```

Programmatically registered test endpoints enter the normal routing tree. They alter which endpoint exists, not how method matching, body binding, request scopes, middleware, or response serialization behave.

Class-level configuration hooks establish defaults for a test class. `IpcTestRunConfiguration` supplies per-run service, endpoint, option, and arrange hooks and is applied after the class defaults, allowing one scenario to replace a dependency without affecting its neighbors.

## Serialization metadata

Known IPC contracts use the same source-generated JSON metadata as production. The test host adds a reflection fallback for small test-only DTOs so a routing test does not have to expand the production AOT serializer surface just to introduce a fixture.

Tests that claim compatibility with an established production wire contract should use the production DTO. The fallback exists for test data, not as an alternate serialization contract.

## Failure and lifetime behavior

The daemon side uses the production connection-processing and worker failure boundaries. A malformed frame, peer disconnect, transport I/O failure, or TLS failure terminates that connection without consuming the worker permanently. Unexpected failures outside the defined connection boundary remain visible to the test rather than being converted into successful cleanup.

Each run links the MSTest cancellation token, any caller token, and the optional adapter-level test timeout. Protocol I/O and request deadlines remain independently configurable, matching production's distinction between an idle I/O timeout and an overall request deadline.

The host owns the daemon application lifetime. Disposal cancels and awaits daemon work before tearing down the dependency-injection containers and broker. Raw streams opened by a test are caller-owned and should be disposed inside the enclosing run.

Keeping those ownership rules strict is important: a passing protocol test must not leave unobserved daemon work or hide a lifecycle failure during teardown.
