using System.Text.Json.Serialization;
using Ufw.Web.Model.V1.Auth;
using Ufw.Web.Model.V1.KnownHosts;
using Ufw.Web.Model.V1.NetworkInterfaces;
using Ufw.Web.Model.V1.RuleMetadata;
using Ufw.Web.Model.V1.RuleTags;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.Api;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(ChangePasswordRequest))]
[JsonSerializable(typeof(AuthTokenResponse))]
[JsonSerializable(typeof(ApiProblemDetails))]
[JsonSerializable(typeof(KnownHostInventoryResponse))]
[JsonSerializable(typeof(CreateKnownHostRequest))]
[JsonSerializable(typeof(UpdateKnownHostRequest))]
[JsonSerializable(typeof(NetworkInterfaceInventoryResponse))]
[JsonSerializable(typeof(RuleInventoryResponse))]
[JsonSerializable(typeof(RuleMetadataMutationResponse))]
[JsonSerializable(typeof(RuleMetadataReconciliationResponse))]
[JsonSerializable(typeof(CleanupRuleMetadataRequest))]
[JsonSerializable(typeof(UpdateRuleMetadataRequest))]
[JsonSerializable(typeof(RuleTagInventoryResponse))]
[JsonSerializable(typeof(CreateRuleTagRequest))]
[JsonSerializable(typeof(UpdateRuleTagRequest))]
[JsonSerializable(typeof(UpdateNetworkInterfaceCommentRequest))]
[JsonSerializable(typeof(UpdateNetworkInterfaceVisibilityRequest))]
internal sealed partial class ClientJsonSerializerContext : JsonSerializerContext;
