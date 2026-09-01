using Microsoft.Extensions.DependencyInjection;
using Ufw.Ipc.Tests.Adapter.Endpoints;

namespace Ufw.Ipc.Tests.Adapter;

/// <summary>
/// Optional per-run overrides layered on top of class-level configuration.
/// </summary>
public sealed class IpcTestRunConfiguration
{
    public Action<IServiceCollection>? ConfigureServerServices { get; init; }

    public Action<IServiceCollection>? ConfigureClientServices { get; init; }

    public Action<ITestEndpointMapBuilder>? ConfigureEndpoints { get; init; }

    public Func<IServiceCollection, CancellationToken, ValueTask>? ConfigureServerServicesAsync { get; init; }

    public Func<IServiceCollection, CancellationToken, ValueTask>? ConfigureClientServicesAsync { get; init; }

    public Func<ITestEndpointMapBuilder, CancellationToken, ValueTask>? ConfigureEndpointsAsync { get; init; }

    public Func<IIpcTestContext, CancellationToken, ValueTask>? ArrangeAsync { get; init; }

    public Action<IpcTestOptions>? ConfigureOptions { get; init; }
}
