using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ufw.Ipc.Client;
using Ufw.Ipc.Client.Configuration;
using Ufw.Ipc.Client.Handlers;
using Ufw.Ipc.Client.Transport;
using Ufw.Ipc.Tests.Adapter.Configuration;
using Ufw.Ipc.Tests.Adapter.Endpoints;
using Ufw.Ipc.Tests.Adapter.Serialization;
using Ufw.Ipc.Tests.Adapter.Transport;
using Ufw.Roslyn.Controllers.Mapping;
using Ufw.Roslyn.Json;
using Ufw.Shared.Ipc.Serialization;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Ipc.Transport.Itp;
using Ufw.Shared.Ipc.Transport.Security;
using Ufw.Systemd.Api.Middleware;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Configuration.Model;
using Ufw.Systemd.Network;
using Ufw.Systemd.Services.Logging;
using ServerTransport = Ufw.Systemd.Transport;

namespace Ufw.Ipc.Tests.Adapter.DependencyInjection;

internal static class IpcTestServiceRegistrar
{
    public static IServiceCollection AddIpcTestServerDefaults(this IServiceCollection services, InProcessTransportBroker broker, TestApiEndpointMap endpointMap, AppSettings appSettings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(broker);
        ArgumentNullException.ThrowIfNull(endpointMap);
        ArgumentNullException.ThrowIfNull(appSettings);

        services.AddSingleton(broker);
        services.AddSingleton<IConfiguration>(new TestConfiguration(appSettings));
        services.AddSingleton<ILogger>(NullLogger.Instance);
        services.AddSingleton(MessageJsonSerializerContext.Default);
        services.AddSingleton(HybridMessageJsonSerializerContext.CreateDefault());
        services.AddSingleton<AotJsonSerializerContext>(static sp => sp.GetRequiredService<HybridMessageJsonSerializerContext>());
        services.AddSingleton(ItpOptions.Default);
        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.AddSingleton<IApiEndpointMap<IRequestMessage, IResponseMessage>>(endpointMap);
        services.AddSingleton<IRequestMiddleware, RequestLoggingMiddleware>();
        services.AddSingleton<IRequestMiddleware, EndpointInvocationMiddleware>();
        services.AddSingleton<IRequestResponsePipeline, RequestResponsePipeline>();
        services.AddSingleton<ITransportSecurityService, NoTransportSecurityService>();
        services.AddSingleton<ServerTransport.ITransportLayerService, InProcessServerTransportService>();
        services.AddSingleton<INetworkApplication, NetworkApplication>();
        services.AddSingleton<INetworkConnectionProcessor, NetworkConnectionProcessor>();
        services.AddSingleton<INetworkApplicationWorker, NetworkApplicationWorker>();
        return services;
    }

    public static IServiceCollection AddIpcTestClientDefaults(this IServiceCollection services, InProcessTransportBroker broker, IpcTestOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(broker);
        ArgumentNullException.ThrowIfNull(options);

        // Satisfy types that still take UfwClientOptions even when transport is replaced.
        services.TryAddSingleton(new UfwClientOptions(
            ServerName: ".",
            PipeName: "/tmp/ufw-ipc-tests.inprocess",
            TlsEnabled: false,
            TlsServerName: null,
            SslProtocols: System.Security.Authentication.SslProtocols.None,
            IoTimeout: options.ClientIoTimeout ?? options.IoTimeout,
            RequestTimeout: options.ClientRequestTimeout ?? options.RequestTimeout));
        services.AddSingleton(broker);
        services.AddUfwClientCoreServices();
        services.AddSingleton(HybridMessageJsonSerializerContext.CreateDefault());
        services.AddSingleton<AotJsonSerializerContext>(static sp => sp.GetRequiredService<HybridMessageJsonSerializerContext>());
        services.AddSingleton<ITransportSecurityService, NoTransportSecurityService>();
        services.AddSingleton<ITransportLayerService, InProcessClientTransportService>();
        return services;
    }
}
