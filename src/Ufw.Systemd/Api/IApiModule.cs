using Jab;
using Ufw.Pipes.Shared.Serialization;
using Ufw.Pipes.Shared.Serialization.Json;
using Ufw.Roslyn.Controllers.Mapping;
using Ufw.Systemd.Api.Controllers;
using Ufw.Systemd.Api.Middleware;

namespace Ufw.Systemd.Api;

[ServiceProviderModule]
[Scoped<RulesController>]
[Singleton<MessageJsonSerializerContext>(Factory = nameof(GetMessageJsonSerializerContext))]
[Singleton<IMessageSerializer, JsonMessageSerializer>]
[Singleton<IRequestMiddleware, RequestValidationMiddleware>]
[Singleton<IRequestMiddleware, RequestLoggingMiddleware>]
[Singleton<IRequestMiddleware, EndpointInvocationMiddleware>]
[Singleton<IRequestResponsePipeline, RequestResponsePipeline>]
[Singleton<IApiEndpointMap<IMessage, IMessage>, UfwApiEndpointMap>]
internal interface IApiModule
{
    internal static MessageJsonSerializerContext GetMessageJsonSerializerContext() => MessageJsonSerializerContext.Default;
}
