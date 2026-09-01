using System.Text.Json;
using System.Text.Json.Serialization;
using Ufw.Ipc.Shared.Model.Requests.Domain;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Model.Responses.Domain;
using Ufw.Ipc.Shared.Protocol;
using Ufw.Roslyn.Json;

namespace Ufw.Ipc.Shared.Serialization.Json;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(ApplicationEnvelope))]
[JsonSerializable(typeof(OkResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(BadRequestResponse))]
[JsonSerializable(typeof(RequestTimeoutResponse))]
[JsonSerializable(typeof(ModelValidationErrorResponse))]
[JsonSerializable(typeof(InternalServerErrorResponse))]
[JsonSerializable(typeof(NotFoundResponse))]
[JsonSerializable(typeof(UnprocessableContentResponse))]
[JsonSerializable(typeof(NotImplementedResponse))]
// domain
[JsonSerializable(typeof(DeleteRuleRequest))]
[JsonSerializable(typeof(RuleListResponse))]
[JsonTypeInfoBindingsGenerator(GenerationMode = BindingsGenerationMode.Optimized)]
public sealed partial class MessageJsonSerializerContext : AotJsonSerializerContext;
