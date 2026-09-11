using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;

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
