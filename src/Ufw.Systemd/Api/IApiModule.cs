using Jab;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Shared.Serialization.Json;
using Ufw.Roslyn.Controllers.Mapping;
using Ufw.Roslyn.Json;
using Ufw.Systemd.Api.Controllers;
using Ufw.Systemd.Api.Middleware;

namespace Ufw.Systemd.Api;

[ServiceProviderModule]
[Scoped<RulesController>]
[Singleton<MessageJsonSerializerContext>(Factory = nameof(GetMessageJsonSerializerContext))]
[Singleton<AotJsonSerializerContext>(Factory = nameof(GetAotJsonSerializerContext))]
[Singleton<IMessageSerializer, JsonMessageSerializer>]
[Singleton<IRequestMiddleware, RequestValidationMiddleware>]
[Singleton<IRequestMiddleware, RequestLoggingMiddleware>]
[Singleton<IRequestMiddleware, EndpointInvocationMiddleware>]
[Singleton<IRequestResponsePipeline, RequestResponsePipeline>]
[Singleton<IApiEndpointMap<IMessage, IMessage>, UfwApiEndpointMap>]
internal interface IApiModule
{
    internal static MessageJsonSerializerContext GetMessageJsonSerializerContext() => MessageJsonSerializerContext.Default;

    internal static AotJsonSerializerContext GetAotJsonSerializerContext() => MessageJsonSerializerContext.Default;
}
