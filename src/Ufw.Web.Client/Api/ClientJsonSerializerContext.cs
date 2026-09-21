using System.Text.Json.Serialization;
using Ufw.Web.Client.Api.Auth.Model;
using Ufw.Web.Client.Api.KnownHosts.Model;
using Ufw.Web.Client.Api.NetworkInterfaces.Model;
using Ufw.Web.Client.Api.RuleMetadata.Model;
using Ufw.Web.Client.Api.RuleTags.Model;
using Ufw.Web.Client.Api.Rules.Model;

namespace Ufw.Web.Client.Api;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LoginRequest))]
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
