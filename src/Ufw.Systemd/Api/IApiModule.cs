using Jab;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Shared.Serialization.Json;
using Ufw.Ipc.Shared.Transport.Itp;
using Ufw.Roslyn.Controllers.Mapping;
using Ufw.Roslyn.Json;
using Ufw.Systemd.Api.Controllers;
using Ufw.Systemd.Api.Middleware;

namespace Ufw.Systemd.Api;

[ServiceProviderModule]
[Scoped<IntentController>]
[Scoped<RulesController>]
[Singleton<MessageJsonSerializerContext>(Factory = nameof(GetMessageJsonSerializerContext))]
[Singleton<AotJsonSerializerContext>(Factory = nameof(GetAotJsonSerializerContext))]
[Singleton<ItpOptions>(Factory = nameof(GetItpOptions))]
[Singleton<IMessageSerializer, JsonMessageSerializer>]
[Singleton<IRequestMiddleware, RequestLoggingMiddleware>]
[Singleton<IRequestMiddleware, EndpointInvocationMiddleware>]
[Singleton<IRequestResponsePipeline, RequestResponsePipeline>]
[Singleton<IApiEndpointMap<IRequestMessage, IResponseMessage>, UfwApiEndpointMap>]
internal interface IApiModule
{
    internal static MessageJsonSerializerContext GetMessageJsonSerializerContext() => MessageJsonSerializerContext.Default;

    internal static AotJsonSerializerContext GetAotJsonSerializerContext() => MessageJsonSerializerContext.Default;

    internal static ItpOptions GetItpOptions() => ItpOptions.Default;
}
