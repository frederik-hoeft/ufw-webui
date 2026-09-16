using System.Text.Json.Serialization;
using Ufw.Client.Api;

namespace Ufw.Client.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(AuthTokenResponse))]
[JsonSerializable(typeof(ApiProblemDetails))]
[JsonSerializable(typeof(KnownHostInventoryResponse))]
[JsonSerializable(typeof(CreateKnownHostRequest))]
[JsonSerializable(typeof(UpdateKnownHostRequest))]
[JsonSerializable(typeof(NetworkInterfaceInventoryResponse))]
[JsonSerializable(typeof(RuleInventoryResponse))]
[JsonSerializable(typeof(RuleMetadataMutationResponse))]
[JsonSerializable(typeof(UpdateRuleMetadataRequest))]
[JsonSerializable(typeof(UpdateNetworkInterfaceCommentRequest))]
[JsonSerializable(typeof(UpdateNetworkInterfaceVisibilityRequest))]
internal sealed partial class ClientJsonSerializerContext : JsonSerializerContext;
