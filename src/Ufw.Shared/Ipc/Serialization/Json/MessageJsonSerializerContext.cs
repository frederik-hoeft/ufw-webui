using System.Text.Json;
using System.Text.Json.Serialization;
using Ufw.Roslyn.Json;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Ipc.Protocol;
using Ufw.Shared.Security.Intent;

namespace Ufw.Shared.Ipc.Serialization.Json;

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
[JsonSerializable(typeof(ConflictResponse))]
[JsonSerializable(typeof(ForbiddenResponse))]
// domain
[JsonSerializable(typeof(FirewallAction))]
[JsonSerializable(typeof(FirewallAddressFamily))]
[JsonSerializable(typeof(FirewallDirection))]
[JsonSerializable(typeof(FirewallProtocol))]
[JsonSerializable(typeof(FirewallRuleSpecification))]
[JsonSerializable(typeof(ListedFirewallRule))]
[JsonSerializable(typeof(AddRulePayload))]
[JsonSerializable(typeof(DeleteRulePayload))]
[JsonSerializable(typeof(ReorderRulesPayload))]
[JsonSerializable(typeof(AddRuleRequest))]
[JsonSerializable(typeof(DeleteRuleRequest))]
[JsonSerializable(typeof(IntentContextResponse))]
[JsonSerializable(typeof(NetworkInterfaceListResponse))]
[JsonSerializable(typeof(RuleListResponse))]
[JsonSerializable(typeof(RuleMutationResponse))]
[JsonTypeInfoBindingsGenerator(GenerationMode = BindingsGenerationMode.Optimized)]
public sealed partial class MessageJsonSerializerContext : AotJsonSerializerContext;
