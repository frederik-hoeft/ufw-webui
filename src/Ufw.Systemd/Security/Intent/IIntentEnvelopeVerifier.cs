using Ufw.Shared.Security.Intent;

namespace Ufw.Systemd.Security.Intent;

internal interface IIntentEnvelopeVerifier
{
    IntentVerificationResult Verify<TPayload>(
        ISignedIntent intent,
        string expectedOperation,
        IIntentPayloadBinder<TPayload> payloadBinder,
        Func<VerifiedIntentEnvelope, TPayload, IntentVerificationResult.Accepted> createAccepted)
        where TPayload : class;
}

internal readonly record struct VerifiedIntentEnvelope(string KeyId, string Nonce, long ExpiresAtUnix);
