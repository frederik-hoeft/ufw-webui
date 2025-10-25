using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ufw.Pipes.Client.Handlers;
using Ufw.Pipes.Client.Transport;
using Ufw.Pipes.Client.Transport.Pipes;
using Ufw.Pipes.Client.Transport.Pipes.Unix;
using Ufw.Pipes.Client.Transport.Security;
using Ufw.Pipes.Client.Transport.Security.CertificateValidation;
using Ufw.Pipes.Shared.Serialization;
using Ufw.Pipes.Shared.Serialization.Json;
using Ufw.Pipes.Shared.Transport.Security;

namespace Ufw.Pipes.Client.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUfwClientServices(this IServiceCollection services, Action<UfwClientBuilder> configureClient)
    {
        ArgumentNullException.ThrowIfNull(configureClient, nameof(configureClient));
        using UfwClientBuilder ufwClientBuilder = new();
        configureClient(ufwClientBuilder);
        UfwClientOptions implementationInstance = ufwClientBuilder.Build();
        services.AddSingleton(implementationInstance);
        services.AddSingleton(MessageJsonSerializerContext.Default);
        services.AddSingleton<IMessageSerializer, JsonMessageSerializer>();
        services.AddSingleton<IResponseMessageHandler, BadRequestResponseHandler>();
        services.AddSingleton<IResponseMessageHandler, ErrorResponseHandler>();
        services.AddSingleton<IResponseMessageHandler, DataResponseHandler>();
        services.AddSingleton<IResponseMessageHandler, ResponseProtocolErrorHandler>();
        services.AddSingleton<ITransportLayerService, NamedPipeClientTransportService>();
        services.AddSingleton<INamedPipeClientStreamFactory, UnixNamedPipeClientStreamFactory>();
        services.AddSingleton<ITransportSecurityService, ClientTransportSecurityService>();
        services.TryAddSingleton<IRemoteCertificateValidationHandler, DefaultRemoteCertificateValidationHandler>();
        services.AddScoped<IUfwClient, UfwClient>();
        return services;
    }
}
