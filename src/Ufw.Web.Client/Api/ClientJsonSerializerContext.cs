using System.Text.Json.Serialization;
using Ufw.Web.Model.V1.Auth;
using Ufw.Web.Model.V1.Errors;
using Ufw.Web.Model.V1.KnownHosts;
using Ufw.Web.Model.V1.NetworkInterfaces;
using Ufw.Web.Model.V1.RuleGroups;
using Ufw.Web.Model.V1.RuleMetadata;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Model.V1.Rules.Intent;
using Ufw.Web.Model.V1.RuleTags;
using Ufw.Web.Model.V1.RuleTemplates;

namespace Ufw.Web.Client.Api;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(ChangePasswordRequest))]
[JsonSerializable(typeof(AuthTokenResponse))]
[JsonSerializable(typeof(AntiforgeryTokenResponse))]
[JsonSerializable(typeof(ApiProblemDetails))]
[JsonSerializable(typeof(LegacyApiErrorMessage))]
[JsonSerializable(typeof(KnownHostInventoryResponse))]
[JsonSerializable(typeof(CreateKnownHostRequest))]
[JsonSerializable(typeof(UpdateKnownHostRequest))]
[JsonSerializable(typeof(NetworkInterfaceInventoryResponse))]
[JsonSerializable(typeof(RuleInventoryResponse))]
[JsonSerializable(typeof(AddRuleIntentRequest))]
[JsonSerializable(typeof(DeleteRuleIntentRequest))]
[JsonSerializable(typeof(BatchDeleteRulesIntentRequest))]
[JsonSerializable(typeof(InsertRuleIntentRequest))]
[JsonSerializable(typeof(ReorderRulesIntentRequest))]
[JsonSerializable(typeof(ReplaceRuleIntentRequest))]
[JsonSerializable(typeof(RuleMetadataMutationResponse))]
[JsonSerializable(typeof(RuleReplacementMutationResponse))]
[JsonSerializable(typeof(RuleGroupInventoryResponse))]
[JsonSerializable(typeof(CreateRuleGroupRequest))]
[JsonSerializable(typeof(UpdateRuleGroupRequest))]
[JsonSerializable(typeof(RuleMetadataReconciliationResponse))]
[JsonSerializable(typeof(CleanupRuleMetadataRequest))]
[JsonSerializable(typeof(UpdateRuleMetadataRequest))]
[JsonSerializable(typeof(RuleTagInventoryResponse))]
[JsonSerializable(typeof(CreateRuleTagRequest))]
[JsonSerializable(typeof(UpdateRuleTagRequest))]
[JsonSerializable(typeof(RuleTemplateInventoryResponse))]
[JsonSerializable(typeof(CreateRuleTemplateRequest))]
[JsonSerializable(typeof(UpdateRuleTemplateRequest))]
[JsonSerializable(typeof(UpdateNetworkInterfaceCommentRequest))]
[JsonSerializable(typeof(UpdateNetworkInterfaceVisibilityRequest))]
internal sealed partial class ClientJsonSerializerContext : JsonSerializerContext;
