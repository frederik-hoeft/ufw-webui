using System.Text.Json;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Configuration;

namespace Ufw.Systemd.Security.Intent;

internal sealed class IntentVerifier
(
    IAuthorizedKeyStore authorizedKeys,
    IDeploymentIdentityProvider deploymentIdentity,
    IConfiguration configuration,
    TimeProvider timeProvider,
    MessageJsonSerializerContext jsonContext
) : IIntentVerifier
{
    public IntentVerificationResult VerifyAdd(ISignedIntent intent) => Verify(
        intent,
        IntentOperations.ADD_RULE,
        ParseAddPayload);

    public IntentVerificationResult VerifyDelete(ISignedIntent intent) => Verify(
        intent,
        IntentOperations.DELETE_RULE,
        ParseDeletePayload);

    public IntentVerificationResult VerifyInsert(ISignedIntent intent) => Verify(
        intent,
        IntentOperations.INSERT_RULE,
        ParseInsertPayload);

    public IntentVerificationResult VerifyReorder(ISignedIntent intent) => Verify(
        intent,
        IntentOperations.REORDER_RULES,
        ParseReorderPayload);

    private IntentVerificationResult Verify(
        ISignedIntent intent,
        string expectedOperation,
        Func<ISignedIntent, PayloadVerification> payloadVerifier)
    {
        ArgumentNullException.ThrowIfNull(intent);
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

        PayloadVerification payloadVerification;
        try
        {
            payloadVerification = payloadVerifier(intent);
        }
        catch (JsonException)
        {
            return Reject(new BadRequestResponse("Signed intent payload is malformed."));
        }
        catch (NotSupportedException)
        {
            return Reject(new BadRequestResponse("Signed intent payload has an unsupported shape."));
        }
        if (payloadVerification.Error is not null)
        {
            return Reject(payloadVerification.Error);
        }

        if (!authorizedKeys.TryGetKey(intent.KeyId, out System.Security.Cryptography.ECDsa? key))
        {
            return Reject(new ForbiddenResponse("Intent was not signed by an authorized key."));
        }

        if (!IntentSigner.Verify(key, payloadVerification.Canonical!, intent.Signature))
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

        return payloadVerification.CreateAccepted!(intent.KeyId, intent.Nonce, expiresAtUnix);
    }

    private PayloadVerification ParseAddPayload(ISignedIntent intent)
    {
        AddRulePayload? payload = intent.Payload.Deserialize(jsonContext.AddRulePayload);
        if (payload?.Rule is null)
        {
            return PayloadVerification.Reject(new BadRequestResponse("Add-rule payload is missing a rule specification."));
        }

        if (!RuleSpecificationValidator.TryValidate(payload.Rule, out ModelValidationErrorResponse? validationError))
        {
            return PayloadVerification.Reject(validationError);
        }

        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(payload.Rule);
        AddRulePayload normalizedPayload = new() { Rule = normalized };
        byte[] canonical = IntentCanonicalizer.CanonicalizeAdd(intent, normalizedPayload);
        return PayloadVerification.Accept(
            canonical,
            (keyId, nonce, expiresAtUnix) => new IntentVerificationResult.AcceptedRuleMutation(
                keyId,
                nonce,
                expiresAtUnix,
                normalized,
                null));
    }

    private PayloadVerification ParseDeletePayload(ISignedIntent intent)
    {
        DeleteRulePayload? payload = intent.Payload.Deserialize(jsonContext.DeleteRulePayload);
        if (payload?.Rule is null || string.IsNullOrWhiteSpace(payload.RuleId))
        {
            return PayloadVerification.Reject(
                new BadRequestResponse("Delete-rule payload must include ruleId and a rule specification."));
        }

        if (!RuleSpecificationValidator.TryValidate(payload.Rule, out ModelValidationErrorResponse? validationError))
        {
            return PayloadVerification.Reject(validationError);
        }

        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(payload.Rule);
        if (normalized.AddressFamily == FirewallAddressFamily.Any)
        {
            return PayloadVerification.Reject(new BadRequestResponse(
                "Delete-rule specifications must use a concrete address family from the current rule listing."));
        }

        string computed = RuleIdentity.Compute(normalized);
        if (!string.Equals(computed, payload.RuleId, StringComparison.Ordinal))
        {
            return PayloadVerification.Reject(new BadRequestResponse("Delete ruleId does not match the supplied rule specification."));
        }

        DeleteRulePayload normalizedPayload = new() { RuleId = payload.RuleId, Rule = normalized };
        byte[] canonical = IntentCanonicalizer.CanonicalizeDelete(intent, normalizedPayload);
        return PayloadVerification.Accept(
            canonical,
            (keyId, nonce, expiresAtUnix) => new IntentVerificationResult.AcceptedRuleMutation(
                keyId,
                nonce,
                expiresAtUnix,
                normalized,
                payload.RuleId));
    }

    private PayloadVerification ParseInsertPayload(ISignedIntent intent)
    {
        InsertRulePayload? payload = intent.Payload.Deserialize(jsonContext.InsertRulePayload);
        if (payload?.Rule is null || string.IsNullOrWhiteSpace(payload.BaselineFingerprint))
        {
            return PayloadVerification.Reject(
                new BadRequestResponse("Insert-rule payload must include a baseline fingerprint and rule specification."));
        }

        if (!RuleSpecificationValidator.TryValidate(payload.Rule, out ModelValidationErrorResponse? validationError))
        {
            return PayloadVerification.Reject(validationError);
        }

        InsertRulePayload verifiedPayload = new()
        {
            BaselineFingerprint = payload.BaselineFingerprint,
            AnchorOccurrenceId = payload.AnchorOccurrenceId,
            Placement = payload.Placement,
            Rule = RuleSpecificationNormalizer.Normalize(payload.Rule),
        };
        try
        {
            RuleInsertionContract.ValidatePayload(verifiedPayload);
        }
        catch (ArgumentException exception)
        {
            return PayloadVerification.Reject(new BadRequestResponse(exception.Message));
        }

        byte[] canonical = IntentCanonicalizer.CanonicalizeInsert(intent, verifiedPayload);
        return PayloadVerification.Accept(
            canonical,
            (keyId, nonce, expiresAtUnix) => new IntentVerificationResult.AcceptedInsertion(
                keyId,
                nonce,
                expiresAtUnix,
                verifiedPayload));
    }

    private PayloadVerification ParseReorderPayload(ISignedIntent intent)
    {
        ReorderRulesPayload? payload = intent.Payload.Deserialize(jsonContext.ReorderRulesPayload);
        if (payload is null
            || string.IsNullOrWhiteSpace(payload.BaselineFingerprint)
            || payload.DesiredOrder is null)
        {
            return PayloadVerification.Reject(
                new BadRequestResponse("Reorder payload must include a baseline fingerprint and desired order."));
        }

        if (!FirewallRuleSnapshotFingerprint.IsValid(payload.BaselineFingerprint))
        {
            return PayloadVerification.Reject(new BadRequestResponse("Reorder baseline fingerprint is malformed."));
        }

        ReorderRulesPayload verifiedPayload = new()
        {
            BaselineFingerprint = payload.BaselineFingerprint,
            DesiredOrder = [.. payload.DesiredOrder],
        };
        byte[] canonical = IntentCanonicalizer.CanonicalizeReorder(intent, verifiedPayload);
        return PayloadVerification.Accept(
            canonical,
            (keyId, nonce, expiresAtUnix) => new IntentVerificationResult.AcceptedReorder(
                keyId,
                nonce,
                expiresAtUnix,
                verifiedPayload));
    }

    private SecurityOptionsSnapshot? ReadSecurity()
    {
        Configuration.Model.SecurityOptions? security = configuration.Settings.Security;
        if (security is null)
        {
            return null;
        }

        return new SecurityOptionsSnapshot(security.MaxIntentAge, security.ClockSkew);
    }

    private static IntentVerificationResult.Rejected Reject(IResponsePayload response) => new(response);

    private sealed record PayloadVerification(
        byte[]? Canonical,
        Func<string, string, long, IntentVerificationResult.Accepted>? CreateAccepted,
        IResponsePayload? Error)
    {
        public static PayloadVerification Accept(
            byte[] canonical,
            Func<string, string, long, IntentVerificationResult.Accepted> createAccepted) =>
            new(canonical, createAccepted, null);

        public static PayloadVerification Reject(IResponsePayload error) => new(null, null, error);
    }

    private readonly record struct SecurityOptionsSnapshot(TimeSpan MaxIntentAge, TimeSpan ClockSkew);
}
