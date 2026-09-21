using System.Text.Json.Serialization;
using Ufw.Web.Client.Features.Authentication.Api;
using Ufw.Web.Client.Features.KnownHosts.Api;
using Ufw.Web.Client.Features.NetworkInterfaces.Api;
using Ufw.Web.Client.Features.Rules.Api;
using Ufw.Web.Client.Infrastructure.Http;

namespace Ufw.Web.Client.Infrastructure.Serialization;

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
