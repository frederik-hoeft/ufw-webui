using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.Features.Rules.Authoring.Workflows;

internal sealed record RuleCreationAddResult(RuleMutationResponse Firewall, string ConfirmedRuleId, ClientError? MetadataError);
