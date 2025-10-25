using System.Text.Json.Serialization;
using Ufw.Pipes.Shared.Model;
using Ufw.Pipes.Shared.Model.Requests.Domain;
using Ufw.Pipes.Shared.Model.Responses;
using Ufw.Pipes.Shared.Model.Responses.Domain;
using Ufw.Roslyn.Json;

namespace Ufw.Pipes.Shared.Serialization.Json;

[JsonSourceGenerationOptions(
    WriteIndented = false, 
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, 
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(object))]
[JsonSerializable(typeof(MessageHeader))]
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