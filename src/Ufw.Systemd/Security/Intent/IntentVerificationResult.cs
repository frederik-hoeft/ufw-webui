using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Security.Intent;

namespace Ufw.Systemd.Security.Intent;

internal abstract record IntentVerificationResult
{
    private IntentVerificationResult()
    {
    }

    internal abstract record Accepted(string KeyId, string Nonce, long ExpiresAtUnix) : IntentVerificationResult;

    internal sealed record AcceptedRuleMutation(
        string KeyId,
        string Nonce,
        long ExpiresAtUnix,
        FirewallRuleSpecification Rule,
        string? RuleId) : Accepted(KeyId, Nonce, ExpiresAtUnix);

    internal sealed record AcceptedReorder(
        string KeyId,
        string Nonce,
        long ExpiresAtUnix,
        ReorderRulesPayload Payload) : Accepted(KeyId, Nonce, ExpiresAtUnix);

    internal sealed record Rejected(IResponsePayload Response) : IntentVerificationResult;
}
