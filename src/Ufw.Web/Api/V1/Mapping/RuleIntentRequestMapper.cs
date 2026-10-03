using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Web.Model.V1.Rules.Intent;

namespace Ufw.Web.Api.V1.Mapping;

internal static class RuleIntentRequestMapper
{
    public static AddRuleRequest ToDaemonRequest(this AddRuleIntentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new AddRuleRequest
        {
            Version = request.Version,
            DeploymentId = request.DeploymentId,
            KeyId = request.KeyId,
            IssuedAtUnix = request.IssuedAtUnix,
            Nonce = request.Nonce,
            Operation = request.Operation,
            Payload = request.Payload,
            Signature = request.Signature,
        };
    }

    public static InsertRuleRequest ToDaemonRequest(this InsertRuleIntentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new InsertRuleRequest
        {
            Version = request.Version,
            DeploymentId = request.DeploymentId,
            KeyId = request.KeyId,
            IssuedAtUnix = request.IssuedAtUnix,
            Nonce = request.Nonce,
            Operation = request.Operation,
            Payload = request.Payload,
            Signature = request.Signature,
        };
    }

    public static ReplaceRuleRequest ToDaemonRequest(this ReplaceRuleIntentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ReplaceRuleRequest
        {
            Version = request.Version,
            DeploymentId = request.DeploymentId,
            KeyId = request.KeyId,
            IssuedAtUnix = request.IssuedAtUnix,
            Nonce = request.Nonce,
            Operation = request.Operation,
            Payload = request.Payload,
            Signature = request.Signature,
        };
    }

    public static ReorderRulesRequest ToDaemonRequest(this ReorderRulesIntentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ReorderRulesRequest
        {
            Version = request.Version,
            DeploymentId = request.DeploymentId,
            KeyId = request.KeyId,
            IssuedAtUnix = request.IssuedAtUnix,
            Nonce = request.Nonce,
            Operation = request.Operation,
            Payload = request.Payload,
            Signature = request.Signature,
        };
    }

    public static BatchDeleteRulesRequest ToDaemonRequest(this BatchDeleteRulesIntentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new BatchDeleteRulesRequest
        {
            Version = request.Version,
            DeploymentId = request.DeploymentId,
            KeyId = request.KeyId,
            IssuedAtUnix = request.IssuedAtUnix,
            Nonce = request.Nonce,
            Operation = request.Operation,
            Payload = request.Payload,
            Signature = request.Signature,
        };
    }

    public static DeleteRuleRequest ToDaemonRequest(this DeleteRuleIntentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new DeleteRuleRequest
        {
            Version = request.Version,
            DeploymentId = request.DeploymentId,
            KeyId = request.KeyId,
            IssuedAtUnix = request.IssuedAtUnix,
            Nonce = request.Nonce,
            Operation = request.Operation,
            Payload = request.Payload,
            Signature = request.Signature,
        };
    }
}
