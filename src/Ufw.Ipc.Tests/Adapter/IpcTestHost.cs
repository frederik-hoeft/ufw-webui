using Microsoft.Extensions.DependencyInjection;
using Ufw.Ipc.Client;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Shared.Transport.Security;
using Ufw.Ipc.Tests.Adapter.Configuration;
using Ufw.Ipc.Tests.Adapter.DependencyInjection;
using Ufw.Ipc.Tests.Adapter.Endpoints;
using Ufw.Ipc.Tests.Adapter.Hosting;
using Ufw.Ipc.Tests.Adapter.Transport;
using Ufw.Systemd.Api.Middleware;
using Ufw.Systemd.Configuration.Model;

namespace Ufw.Ipc.Tests.Adapter;

/// <summary>
/// Owns one isolated in-process IPC client/server pair for the duration of a single test run.
/// </summary>
internal sealed class IpcTestHost : IAsyncDisposable
{
    private readonly InProcessTransportBroker _broker;
    private readonly ServiceProvider _serverProvider;
    private readonly ServiceProvider _clientProvider;
    private readonly AsyncServiceScope _clientScope;
    private readonly CancellationTokenSource _hostCts;
    private readonly Task[] _workerTasks;
    private readonly IUfwClient _client;
    private readonly IMessageSerializer _messageSerializer;
    private readonly IRequestResponsePipeline _pipeline;
    private readonly ITransportSecurityService _clientTransportSecurity;
    private bool _disposed;

    private IpcTestHost(
        InProcessTransportBroker broker,
        ServiceProvider serverProvider,
        ServiceProvider clientProvider,
        AsyncServiceScope clientScope,
        CancellationTokenSource hostCts,
        Task[] workerTasks,
        IUfwClient client,
        IMessageSerializer messageSerializer,
        IRequestResponsePipeline pipeline,
        ITransportSecurityService clientTransportSecurity)
    {
        _broker = broker;
        _serverProvider = serverProvider;
        _clientProvider = clientProvider;
        _clientScope = clientScope;
        _hostCts = hostCts;
        _workerTasks = workerTasks;
        _client = client;
        _messageSerializer = messageSerializer;
        _pipeline = pipeline;
        _clientTransportSecurity = clientTransportSecurity;
    }

    public static async ValueTask<IpcTestHost> StartAsync(
        IpcTestOptions options,
        Action<IServiceCollection>? configureServerServices,
        Action<IServiceCollection>? configureClientServices,
        Action<ITestEndpointMapBuilder>? configureEndpoints,
        Func<IServiceCollection, CancellationToken, ValueTask>? configureServerServicesAsync,
        Func<IServiceCollection, CancellationToken, ValueTask>? configureClientServicesAsync,
        Func<ITestEndpointMapBuilder, CancellationToken, ValueTask>? configureEndpointsAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        InProcessTransportBroker broker = new();
        ServiceProvider? serverProvider = null;
        ServiceProvider? clientProvider = null;
        AsyncServiceScope clientScope = default;
        bool clientScopeCreated = false;
        CancellationTokenSource? hostCts = null;
        Task[]? workerTasks = null;

        try
        {
            TestEndpointMapBuilder endpointBuilder = new();
            if (configureEndpoints is not null)
            {
                configureEndpoints(endpointBuilder);
            }

            if (configureEndpointsAsync is not null)
            {
                await configureEndpointsAsync(endpointBuilder, cancellationToken).ConfigureAwait(false);
            }

            TestApiEndpointMap endpointMap = endpointBuilder.Build();
            AppSettings appSettings = TestAppSettingsFactory.Create(
                requestTimeout: options.RequestTimeout,
                maxConnections: Math.Max(1, options.WorkerCount),
                debugMode: options.DebugMode);

            ServiceCollection serverServices = new();
            serverServices.AddIpcTestServerDefaults(broker, endpointMap, appSettings);
            if (configureServerServices is not null)
            {
                configureServerServices(serverServices);
            }

            if (configureServerServicesAsync is not null)
            {
                await configureServerServicesAsync(serverServices, cancellationToken).ConfigureAwait(false);
            }

            ServiceCollection clientServices = new();
            clientServices.AddIpcTestClientDefaults(broker);
            if (configureClientServices is not null)
            {
                configureClientServices(clientServices);
            }

            if (configureClientServicesAsync is not null)
            {
                await configureClientServicesAsync(clientServices, cancellationToken).ConfigureAwait(false);
            }

            serverProvider = serverServices.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true,
            });
            clientProvider = clientServices.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true,
            });

            clientScope = clientProvider.CreateAsyncScope();
            clientScopeCreated = true;

            hostCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            int workerCount = Math.Max(1, options.WorkerCount);
            workerTasks = new Task[workerCount];
            for (int i = 0; i < workerCount; i++)
            {
                IpcTestServerWorker worker = serverProvider.GetRequiredService<IpcTestServerWorker>();
                workerTasks[i] = worker.ServeAsync(hostCts.Token);
            }

            IUfwClient client = clientScope.ServiceProvider.GetRequiredService<IUfwClient>();
            IMessageSerializer messageSerializer = serverProvider.GetRequiredService<IMessageSerializer>();
            IRequestResponsePipeline pipeline = serverProvider.GetRequiredService<IRequestResponsePipeline>();
            ITransportSecurityService clientTransportSecurity = clientProvider.GetRequiredService<ITransportSecurityService>();

            return new IpcTestHost(
                broker,
                serverProvider,
                clientProvider,
                clientScope,
                hostCts,
                workerTasks,
                client,
                messageSerializer,
                pipeline,
                clientTransportSecurity);
        }
        catch
        {
            if (hostCts is not null)
            {
                await hostCts.CancelAsync().ConfigureAwait(false);
            }

            if (workerTasks is not null)
            {
                try
                {
                    await Task.WhenAll(workerTasks).ConfigureAwait(false);
                }
                catch
                {
                    // ignored during startup failure cleanup
                }
            }

            hostCts?.Dispose();

            if (clientScopeCreated)
            {
                await clientScope.DisposeAsync().ConfigureAwait(false);
            }

            if (clientProvider is not null)
            {
                await clientProvider.DisposeAsync().ConfigureAwait(false);
            }

            if (serverProvider is not null)
            {
                await serverProvider.DisposeAsync().ConfigureAwait(false);
            }

            await broker.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public IIpcTestContext CreateContext() =>
        new IpcTestContext(
            _client,
            _serverProvider,
            _clientProvider,
            _messageSerializer,
            _broker,
            _pipeline,
            _clientTransportSecurity);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            await _hostCts.CancelAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }

        try
        {
            await Task.WhenAll(_workerTasks).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Best-effort join; continue deterministic disposal of owned resources.
        }
        catch (Exception)
        {
            // Worker faults must not prevent disposal of DI containers and the broker.
        }

        await _clientScope.DisposeAsync().ConfigureAwait(false);
        await _clientProvider.DisposeAsync().ConfigureAwait(false);
        await _serverProvider.DisposeAsync().ConfigureAwait(false);
        await _broker.DisposeAsync().ConfigureAwait(false);
        _hostCts.Dispose();
    }
}
