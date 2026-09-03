using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Domain.Rules;

namespace Ufw.Systemd.Security.Intent;

internal abstract record IntentVerificationResult
{
    private IntentVerificationResult()
    {
    }

    internal sealed record Accepted(string KeyId, string Nonce, long ExpiresAtUnix, FirewallRuleSpecification Rule, string? RuleId)
        : IntentVerificationResult;

    internal sealed record Rejected(IResponsePayload Response) : IntentVerificationResult;
}
