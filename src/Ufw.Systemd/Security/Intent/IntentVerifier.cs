using System.Text.Json;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Security.Intent;
using Ufw.Ipc.Shared.Serialization.Json;
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
        expectedOperation: IntentOperations.ADD_RULE,
        payloadFactory: static (signed, context) =>
        {
            AddRulePayload? payload = signed.Payload.Deserialize(context.AddRulePayload);
            if (payload?.Rule is null)
            {
                return (null, null, new BadRequestResponse("Add-rule payload is missing a rule specification."));
            }

            return (payload.Rule, null, null);
        });

    public IntentVerificationResult VerifyDelete(ISignedIntent intent) => Verify(
        intent,
        expectedOperation: IntentOperations.DELETE_RULE,
        payloadFactory: static (signed, context) =>
        {
            DeleteRulePayload? payload = signed.Payload.Deserialize(context.DeleteRulePayload);
            if (payload?.Rule is null || string.IsNullOrWhiteSpace(payload.RuleId))
            {
                return (null, null, new BadRequestResponse("Delete-rule payload must include ruleId and a rule specification."));
            }

            return (payload.Rule, payload.RuleId, null);
        });

    private IntentVerificationResult Verify(
        ISignedIntent intent,
        string expectedOperation,
        Func<ISignedIntent, MessageJsonSerializerContext, (FirewallRuleSpecification? Rule, string? RuleId, IResponsePayload? Error)> payloadFactory)
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

        FirewallRuleSpecification? rule;
        string? ruleId;
        IResponsePayload? payloadError;
        try
        {
            (rule, ruleId, payloadError) = payloadFactory(intent, jsonContext);
        }
        catch (JsonException)
        {
            return Reject(new BadRequestResponse("Signed intent payload is malformed."));
        }
        catch (NotSupportedException)
        {
            return Reject(new BadRequestResponse("Signed intent payload has an unsupported shape."));
        }
        if (payloadError is not null)
        {
            return Reject(payloadError);
        }

        if (rule is null)
        {
            return Reject(new BadRequestResponse("Signed intent payload could not be read."));
        }

        if (!RuleSpecificationValidator.TryValidate(rule, out ModelValidationErrorResponse? validationError))
        {
            return Reject(validationError);
        }

        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(rule);
        if (ruleId is not null)
        {
            if (normalized.AddressFamily == FirewallAddressFamily.Any)
            {
                return Reject(new BadRequestResponse(
                    "Delete-rule specifications must use a concrete address family from the current rule listing."));
            }

            string computed = RuleIdentity.Compute(normalized);
            if (!string.Equals(computed, ruleId, StringComparison.Ordinal))
            {
                return Reject(new BadRequestResponse("Delete ruleId does not match the supplied rule specification."));
            }
        }

        byte[] canonical = ruleId is null
            ? IntentCanonicalizer.CanonicalizeAdd(intent, new AddRulePayload { Rule = normalized })
            : IntentCanonicalizer.CanonicalizeDelete(intent, new DeleteRulePayload { RuleId = ruleId, Rule = normalized });

        if (!authorizedKeys.TryGetKey(intent.KeyId, out System.Security.Cryptography.ECDsa? key))
        {
            return Reject(new ForbiddenResponse("Intent was not signed by an authorized key."));
        }

        if (!IntentSigner.Verify(key, canonical, intent.Signature))
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

        return new IntentVerificationResult.Accepted(intent.KeyId, intent.Nonce, expiresAtUnix, normalized, ruleId);
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

    private readonly record struct SecurityOptionsSnapshot(TimeSpan MaxIntentAge, TimeSpan ClockSkew);
}
