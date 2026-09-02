using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Ipc.Shared.Security.Intent;

namespace Ufw.Systemd.Security.Intent;

internal interface IIntentVerifier
{
    IntentVerificationResult VerifyAdd(ISignedIntent intent);

    IntentVerificationResult VerifyDelete(ISignedIntent intent);
}

internal abstract record IntentVerificationResult
{
    private IntentVerificationResult()
    {
    }

    internal sealed record Accepted(string KeyId, string Nonce, long ExpiresAtUnix, FirewallRuleSpecification Rule, string? RuleId)
        : IntentVerificationResult;

    internal sealed record Rejected(IResponsePayload Response) : IntentVerificationResult;
}
