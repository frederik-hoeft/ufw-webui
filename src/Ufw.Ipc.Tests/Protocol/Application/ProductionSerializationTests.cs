using System.Text.Json;
using Ufw.Ipc.Shared.Model.Requests.Domain;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Model.Responses.Domain;
using Ufw.Ipc.Shared.Protocol;
using Ufw.Ipc.Shared.Serialization.Json;
using Ufw.Roslyn.Json;
using Ufw.Systemd.Api;

namespace Ufw.Ipc.Tests.Protocol.Application;

[TestClass]
public sealed class ProductionSerializationTests
{
    [TestMethod]
    public void DaemonApplicationCodec_UsesProductionSourceGeneratedContext()
    {
        AotJsonSerializerContext context = IApiModule.GetAotJsonSerializerContext();

        Assert.AreSame(MessageJsonSerializerContext.Default, context);
        Assert.IsInstanceOfType<MessageJsonSerializerContext>(context);
    }

    [TestMethod]
    public void ProductionContext_ContainsRequiredApplicationProtocolMetadata()
    {
        MessageJsonSerializerContext context = MessageJsonSerializerContext.Default;

        Assert.IsNotNull(context.GetTypeInfoOrDefault<ApplicationEnvelope>());
        Assert.IsNotNull(context.GetTypeInfoOrDefault<JsonElement>());
        Assert.IsNotNull(context.GetTypeInfoOrDefault<OkResponse>());
        Assert.IsNotNull(context.GetTypeInfoOrDefault<ErrorResponse>());
        Assert.IsNotNull(context.GetTypeInfoOrDefault<BadRequestResponse>());
        Assert.IsNotNull(context.GetTypeInfoOrDefault<ModelValidationErrorResponse>());
        Assert.IsNotNull(context.GetTypeInfoOrDefault<InternalServerErrorResponse>());
        Assert.IsNotNull(context.GetTypeInfoOrDefault<NotFoundResponse>());
        Assert.IsNotNull(context.GetTypeInfoOrDefault<NotImplementedResponse>());
        Assert.IsNotNull(context.GetTypeInfoOrDefault<DeleteRuleRequest>());
        Assert.IsNotNull(context.GetTypeInfoOrDefault<RuleListResponse>());
    }
}
