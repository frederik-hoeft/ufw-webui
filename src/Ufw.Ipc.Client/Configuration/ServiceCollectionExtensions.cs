using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ufw.Ipc.Client.Handlers;
using Ufw.Ipc.Client.Transport;
using Ufw.Ipc.Client.Transport.Pipes;
using Ufw.Ipc.Client.Transport.Security;
using Ufw.Ipc.Client.Transport.Security.CertificateValidation;
using Ufw.Roslyn.Json;
using Ufw.Shared.Ipc.Serialization;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Ipc.Transport.Itp;
using Ufw.Shared.Ipc.Transport.Security;
using Ufw.Shared.Security.Certificates;

namespace Ufw.Ipc.Client.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUfwClientServices(this IServiceCollection services, Action<UfwClientBuilder> configureClient)
    {
        ArgumentNullException.ThrowIfNull(configureClient, nameof(configureClient));
        using UfwClientBuilder ufwClientBuilder = new();
        configureClient(ufwClientBuilder);
        UfwClientOptions implementationInstance = ufwClientBuilder.Build();
        services.AddSingleton(implementationInstance);
        services.AddUfwClientCoreServices();
        services.AddSingleton<ITransportLayerService, NamedPipeClientTransportService>();
        services.AddSingleton<INamedPipeClientStreamFactory, NamedPipeClientStreamFactory>();
        services.AddSingleton<ITransportSecurityService, ClientTransportSecurityService>();
        services.TryAddSingleton<ICertificateLoader, PemCertificateLoader>();
        services.TryAddSingleton<IRemoteCertificateValidationHandler, DefaultRemoteCertificateValidationHandler>();
        return services;
    }

    internal static IServiceCollection AddUfwClientCoreServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(MessageJsonSerializerContext.Default);
        services.AddSingleton<AotJsonSerializerContext>(static _ => MessageJsonSerializerContext.Default);
        services.AddSingleton(ItpOptions.Default);
        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.AddSingleton<IClientMessageExchange, ClientMessageExchange>();
        services.AddSingleton<IResponseMessageHandler, BadRequestResponseHandler>();
        services.AddSingleton<IResponseMessageHandler, ErrorResponseHandler>();
        services.AddSingleton<IResponseMessageHandler, DataResponseHandler>();
        services.AddSingleton<IResponseMessageHandler, ResponseProtocolErrorHandler>();
        services.AddScoped<IUfwClient, UfwClient>();
        return services;
    }
}
