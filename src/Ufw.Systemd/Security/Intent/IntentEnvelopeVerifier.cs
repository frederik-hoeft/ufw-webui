using System.Text.Json;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Configuration;

namespace Ufw.Systemd.Security.Intent;

internal sealed class IntentEnvelopeVerifier
(
    IAuthorizedKeyStore authorizedKeys,
    IDeploymentIdentityProvider deploymentIdentity,
    IConfiguration configuration,
    TimeProvider timeProvider
) : IIntentEnvelopeVerifier
{
    public IntentVerificationResult Verify<TPayload>(
        ISignedIntent intent,
        string expectedOperation,
        IIntentPayloadBinder<TPayload> payloadBinder,
        Func<VerifiedIntentEnvelope, TPayload, IntentVerificationResult.Accepted> createAccepted)
        where TPayload : class
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(payloadBinder);
        ArgumentNullException.ThrowIfNull(createAccepted);

        if (intent.Version != IntentProtocol.VERSION)
        {
            return Reject(new BadRequestResponse($"Unsupported intent version '{intent.Version}'."));
        }

        if (string.IsNullOrWhiteSpace(intent.DeploymentId)
            || string.IsNullOrWhiteSpace(intent.KeyId)
            || string.IsNullOrWhiteSpace(intent.Nonce)
            || string.IsNullOrWhiteSpace(intent.Operation)
            || string.IsNullOrWhiteSpace(intent.Signature)
            || intent.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return Reject(new BadRequestResponse("Signed intent is missing required fields."));
        }

        if (!string.Equals(intent.DeploymentId, deploymentIdentity.GetDeploymentId(), StringComparison.Ordinal))
        {
            return Reject(new ForbiddenResponse("Intent is not valid for this daemon deployment."));
        }

        if (!string.Equals(intent.Operation, expectedOperation, StringComparison.Ordinal))
        {
            return Reject(new BadRequestResponse($"Intent operation '{intent.Operation}' is not valid for this endpoint."));
        }

        if (!IntentSigner.TryDecodeBase64Url(intent.Nonce, out byte[] nonceBytes)
            || nonceBytes.Length < IntentProtocol.MINIMUM_NONCE_SIZE_BYTES)
        {
            return Reject(new BadRequestResponse("Intent nonce is not a valid base64url value of sufficient length."));
        }

        IntentPayloadBindingResult<TPayload> payloadBinding;
        try
        {
            payloadBinding = payloadBinder.Bind(intent);
        }
        catch (JsonException)
        {
            return Reject(new BadRequestResponse("Signed intent payload is malformed."));
        }
        catch (NotSupportedException)
        {
            return Reject(new BadRequestResponse("Signed intent payload has an unsupported shape."));
        }

        if (payloadBinding is IntentPayloadBindingResult<TPayload>.Rejected rejected)
        {
            return Reject(rejected.Response);
        }

        IntentPayloadBindingResult<TPayload>.Accepted accepted = (IntentPayloadBindingResult<TPayload>.Accepted)payloadBinding;
        if (!authorizedKeys.TryGetKey(intent.KeyId, out System.Security.Cryptography.ECDsa? key))
        {
            return Reject(new ForbiddenResponse("Intent was not signed by an authorized key."));
        }

        if (!IntentSigner.Verify(key, accepted.Canonical, intent.Signature))
        {
            return Reject(new ForbiddenResponse("Intent signature is invalid."));
        }

        if (ReadSecurity() is not { } security)
        {
            return Reject(new InternalServerErrorResponse("Daemon security configuration is not available."));
        }

        long maxAgeSeconds = (long)Math.Ceiling(security.MaxIntentAge.TotalSeconds);
        long skewSeconds = (long)Math.Ceiling(security.ClockSkew.TotalSeconds);
        long now = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        long expiresAtUnix;
        long latestAcceptedIssueTime;
        try
        {
            expiresAtUnix = checked(intent.IssuedAtUnix + maxAgeSeconds + skewSeconds);
            latestAcceptedIssueTime = checked(now + skewSeconds);
        }
        catch (OverflowException)
        {
            return Reject(new BadRequestResponse("Intent timestamp is outside the supported range."));
        }

        if (intent.IssuedAtUnix > latestAcceptedIssueTime)
        {
            return Reject(new ForbiddenResponse("Intent timestamp is in the future."));
        }

        // Intent validity is the half-open interval ending at expiresAtUnix.
        // The replay store retains the nonce until the same boundary, so there
        // is no instant at which an intent is still valid after its nonce expires.
        if (now >= expiresAtUnix)
        {
            return Reject(new ForbiddenResponse("Intent has expired."));
        }

        return createAccepted(new VerifiedIntentEnvelope(intent.KeyId, intent.Nonce, expiresAtUnix), accepted.Payload);
    }

    private SecurityOptionsSnapshot? ReadSecurity()
    {
        Configuration.Model.SecurityOptions? security = configuration.Settings.Security;
        return security is null ? null : new SecurityOptionsSnapshot(security.MaxIntentAge, security.ClockSkew);
    }

    private static IntentVerificationResult.Rejected Reject(IResponsePayload response) => new(response);

    private readonly record struct SecurityOptionsSnapshot(TimeSpan MaxIntentAge, TimeSpan ClockSkew);
}
