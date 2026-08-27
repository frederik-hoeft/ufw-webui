using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ufw.Ipc.Client;
using Ufw.Ipc.Client.Configuration;
using Ufw.Ipc.Client.Handlers;
using Ufw.Ipc.Client.Transport;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Shared.Serialization.Json;
using Ufw.Ipc.Shared.Transport.Security;
using Ufw.Ipc.Tests.Adapter.Configuration;
using Ufw.Ipc.Tests.Adapter.Endpoints;
using Ufw.Ipc.Tests.Adapter.Hosting;
using Ufw.Ipc.Tests.Adapter.Serialization;
using Ufw.Ipc.Tests.Adapter.Transport;
using Ufw.Roslyn.Controllers.Mapping;
using Ufw.Roslyn.Json;
using Ufw.Systemd.Api.Middleware;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Configuration.Model;
using Ufw.Systemd.Services.Logging;
using ServerTransport = Ufw.Systemd.Transport;

namespace Ufw.Ipc.Tests.Adapter.DependencyInjection;

internal static class IpcTestServiceRegistrar
{
    public static IServiceCollection AddIpcTestServerDefaults(
        this IServiceCollection services,
        InProcessTransportBroker broker,
        TestApiEndpointMap endpointMap,
        AppSettings appSettings)
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
        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.AddSingleton<IApiEndpointMap<IMessage, IMessage>>(endpointMap);
        services.AddSingleton<IRequestMiddleware, RequestValidationMiddleware>();
        services.AddSingleton<IRequestMiddleware, RequestLoggingMiddleware>();
        services.AddSingleton<IRequestMiddleware, EndpointInvocationMiddleware>();
        services.AddSingleton<IRequestResponsePipeline, RequestResponsePipeline>();
        services.AddSingleton<ITransportSecurityService, NoTransportSecurityService>();
        services.AddSingleton<ServerTransport.ITransportLayerService, InProcessServerTransportService>();
        services.AddTransient<IpcTestServerWorker>();
        return services;
    }

    public static IServiceCollection AddIpcTestClientDefaults(
        this IServiceCollection services,
        InProcessTransportBroker broker)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(broker);

        // Satisfy types that still take UfwClientOptions even when transport is replaced.
        services.TryAddSingleton(new UfwClientOptions(ServerName: ".", PipeName: "/tmp/ufw-ipc-tests.inprocess", SslProtocols: System.Security.Authentication.SslProtocols.None));
        services.AddSingleton(broker);
        services.AddSingleton(MessageJsonSerializerContext.Default);
        services.AddSingleton(HybridMessageJsonSerializerContext.CreateDefault());
        services.AddSingleton<AotJsonSerializerContext>(static sp => sp.GetRequiredService<HybridMessageJsonSerializerContext>());
        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.AddSingleton<IResponseMessageHandler, BadRequestResponseHandler>();
        services.AddSingleton<IResponseMessageHandler, ErrorResponseHandler>();
        services.AddSingleton<IResponseMessageHandler, DataResponseHandler>();
        services.AddSingleton<IResponseMessageHandler, ResponseProtocolErrorHandler>();
        services.AddSingleton<ITransportSecurityService, NoTransportSecurityService>();
        services.AddSingleton<ITransportLayerService, InProcessClientTransportService>();
        services.AddScoped<IUfwClient, UfwClient>();
        return services;
    }
}
