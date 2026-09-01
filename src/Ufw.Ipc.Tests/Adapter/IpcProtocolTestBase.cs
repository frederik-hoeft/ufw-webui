using Microsoft.Extensions.DependencyInjection;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Tests.Adapter.Endpoints;

namespace Ufw.Ipc.Tests.Adapter;

/// <summary>
/// Abstract facade for protocol-level IPC unit and integration tests.
/// Concrete test classes override configuration hooks and invoke <see cref="RunAsync"/> helpers
/// so arrange/act/assert stays free of host boilerplate.
/// </summary>
public abstract class IpcProtocolTestBase
{
    /// <summary>
    /// MSTest context for the currently executing test. Its cancellation token is linked into every test run.
    /// </summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Class-level server DI customization. Invoked for every <see cref="RunAsync"/> call.
    /// </summary>
    protected virtual ValueTask ConfigureServerServicesAsync(IServiceCollection services, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    /// <summary>
    /// Class-level client DI customization. Invoked for every <see cref="RunAsync"/> call.
    /// </summary>
    protected virtual ValueTask ConfigureClientServicesAsync(IServiceCollection services, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    /// <summary>
    /// Class-level endpoint map customization. Invoked for every <see cref="RunAsync"/> call.
    /// </summary>
    protected virtual ValueTask ConfigureEndpointsAsync(ITestEndpointMapBuilder endpoints, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    /// <summary>
    /// Class-level host option defaults. Invoked for every <see cref="RunAsync"/> call before per-run option overrides.
    /// </summary>
    protected virtual ValueTask ConfigureOptionsAsync(IpcTestOptions options, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    /// <summary>
    /// Runs <paramref name="actAsync"/> inside a freshly built, isolated IPC host.
    /// </summary>
    protected async ValueTask RunAsync(
        Func<IIpcTestContext, CancellationToken, ValueTask> actAsync,
        IpcTestRunConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actAsync);
        _ = await RunCoreAsync(
            async (context, ct) =>
            {
                await actAsync(context, ct).ConfigureAwait(false);
                return true;
            },
            configuration,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs <paramref name="actAsync"/> inside a freshly built, isolated IPC host and returns its result.
    /// </summary>
    protected ValueTask<T> RunAsync<T>(
        Func<IIpcTestContext, CancellationToken, ValueTask<T>> actAsync,
        IpcTestRunConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actAsync);
        return RunCoreAsync(actAsync, configuration, cancellationToken);
    }

    /// <summary>
    /// Convenience overload: per-run endpoint registration without constructing <see cref="IpcTestRunConfiguration"/>.
    /// </summary>
    protected ValueTask RunAsync(
        Func<IIpcTestContext, CancellationToken, ValueTask> actAsync,
        Action<ITestEndpointMapBuilder> configureEndpoints,
        Func<IIpcTestContext, CancellationToken, ValueTask>? arrangeAsync = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configureEndpoints);
        return RunAsync(
            actAsync,
            new IpcTestRunConfiguration
            {
                ConfigureEndpoints = configureEndpoints,
                ArrangeAsync = arrangeAsync,
            },
            cancellationToken);
    }

    /// <summary>
    /// Convenience overload: per-run server DI customization.
    /// </summary>
    protected ValueTask RunAsync(
        Func<IIpcTestContext, CancellationToken, ValueTask> actAsync,
        Action<IServiceCollection> configureServerServices,
        Action<ITestEndpointMapBuilder>? configureEndpoints = null,
        Func<IIpcTestContext, CancellationToken, ValueTask>? arrangeAsync = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configureServerServices);
        return RunAsync(
            actAsync,
            new IpcTestRunConfiguration
            {
                ConfigureServerServices = configureServerServices,
                ConfigureEndpoints = configureEndpoints,
                ArrangeAsync = arrangeAsync,
            },
            cancellationToken);
    }

    /// <summary>
    /// Full-stack typed GET/DELETE-style round-trip helper.
    /// </summary>
    protected ValueTask<TResponse> SendAsync<TResponse>(
        RequestMethod method,
        string route,
        IpcTestRunConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse> =>
        RunAsync(
            (context, ct) => context.SendAsync<TResponse>(method, route, ct),
            configuration,
            cancellationToken);

    /// <summary>
    /// Full-stack typed request/response round-trip helper.
    /// </summary>
    protected ValueTask<TResponse> SendAsync<TRequest, TResponse>(
        RequestMethod method,
        string route,
        TRequest request,
        IpcTestRunConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
        where TResponse : IEquatable<TResponse> =>
        RunAsync(
            (context, ct) => context.SendAsync<TRequest, TResponse>(method, route, request, ct),
            configuration,
            cancellationToken);

    private async ValueTask<T> RunCoreAsync<T>(
        Func<IIpcTestContext, CancellationToken, ValueTask<T>> actAsync,
        IpcTestRunConfiguration? configuration,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource runCts = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.CancellationToken,
            cancellationToken);
        CancellationToken runToken = runCts.Token;

        IpcTestOptions options = new();
        await ConfigureOptionsAsync(options, runToken).ConfigureAwait(false);
        configuration?.ConfigureOptions?.Invoke(options);

        if (options.TestTimeout is { } testTimeout && testTimeout > TimeSpan.Zero && testTimeout != Timeout.InfiniteTimeSpan)
        {
            runCts.CancelAfter(testTimeout);
        }

        await using IpcTestHost host = await IpcTestHost.StartAsync(
            options,
            configureServerServices: null,
            configureClientServices: null,
            configureEndpoints: null,
            configureServerServicesAsync: async (services, ct) =>
            {
                await ConfigureServerServicesAsync(services, ct).ConfigureAwait(false);
                configuration?.ConfigureServerServices?.Invoke(services);
                if (configuration?.ConfigureServerServicesAsync is { } perRun)
                {
                    await perRun(services, ct).ConfigureAwait(false);
                }
            },
            configureClientServicesAsync: async (services, ct) =>
            {
                await ConfigureClientServicesAsync(services, ct).ConfigureAwait(false);
                configuration?.ConfigureClientServices?.Invoke(services);
                if (configuration?.ConfigureClientServicesAsync is { } perRun)
                {
                    await perRun(services, ct).ConfigureAwait(false);
                }
            },
            configureEndpointsAsync: async (endpoints, ct) =>
            {
                await ConfigureEndpointsAsync(endpoints, ct).ConfigureAwait(false);
                configuration?.ConfigureEndpoints?.Invoke(endpoints);
                if (configuration?.ConfigureEndpointsAsync is { } perRun)
                {
                    await perRun(endpoints, ct).ConfigureAwait(false);
                }
            },
            cancellationToken: runToken).ConfigureAwait(false);

        IIpcTestContext context = host.CreateContext();
        if (configuration?.ArrangeAsync is { } arrangeAsync)
        {
            await arrangeAsync(context, runToken).ConfigureAwait(false);
        }

        return await actAsync(context, runToken).ConfigureAwait(false);
    }
}
