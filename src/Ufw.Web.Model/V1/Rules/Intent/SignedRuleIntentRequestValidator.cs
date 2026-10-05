using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Model.V1.Errors;

namespace Ufw.Web.Model.V1.Rules.Intent;

internal static class SignedRuleIntentRequestValidator
{
    private const int P256_P1363_SIGNATURE_SIZE_BYTES = 64;
    private static readonly string[] s_nestedRuleProperties = ["rule", "replacementRule"];
    private static readonly string[] s_addPayloadProperties = ["rule"];
    private static readonly string[] s_deletePayloadProperties = ["ruleId", "rule"];
    private static readonly string[] s_batchDeletePayloadProperties = ["baselineFingerprint", "occurrenceIds"];
    private static readonly string[] s_insertPayloadProperties = ["baselineFingerprint", "anchorOccurrenceId", "placement", "rule"];
    private static readonly string[] s_replacePayloadProperties = ["baselineFingerprint", "targetOccurrenceId", "originalRuleId", "replacementRule"];
    private static readonly string[] s_reorderPayloadProperties = ["baselineFingerprint", "desiredOrder"];
    private static readonly HashSet<string> s_ruleProperties =
    [
        "action", "addressFamily", "direction", "protocol", "source", "sourcePorts", "sourceInterface",
        "destination", "destinationPorts", "destinationInterface", "comment",
    ];
    private static readonly string[] s_requiredRuleProperties = ["action", "addressFamily", "direction", "protocol", "source", "destination"];

    public static IEnumerable<ApiValidationError> ValidateEnvelope(SignedRuleIntentRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.DeploymentId)
            && (!IntentSigner.TryDecodeBase64Url(request.DeploymentId, out byte[] deploymentId) || deploymentId.Length != IntentProtocol.DEPLOYMENT_ID_SIZE_BYTES))
        {
            yield return Invalid(nameof(request.DeploymentId), "Deployment ID must be a base64url deployment identity.");
        }

        if (!string.IsNullOrWhiteSpace(request.KeyId) && !IsKeyId(request.KeyId))
        {
            yield return Invalid(nameof(request.KeyId), "Key ID must be a sha256-prefixed base64url SHA-256 digest.");
        }

        if (!string.IsNullOrWhiteSpace(request.Nonce)
            && (!IntentSigner.TryDecodeBase64Url(request.Nonce, out byte[] nonce) || nonce.Length < IntentProtocol.MINIMUM_NONCE_SIZE_BYTES))
        {
            yield return Invalid(nameof(request.Nonce), $"Nonce must be base64url and decode to at least {IntentProtocol.MINIMUM_NONCE_SIZE_BYTES} bytes.");
        }

        if (!string.IsNullOrWhiteSpace(request.Signature)
            && (!IntentSigner.TryDecodeBase64Url(request.Signature, out byte[] signature) || signature.Length != P256_P1363_SIGNATURE_SIZE_BYTES))
        {
            yield return Invalid(nameof(request.Signature), "Signature must be a base64url IEEE-P1363 P-256 signature.");
        }
    }

    public static IEnumerable<ApiValidationError> ValidateAdd(JsonElement element)
    {
        if (!TryBind(element, MessageJsonSerializerContext.Default.AddRulePayload, s_addPayloadProperties, out AddRulePayload? payload, out ApiValidationError? error))
        {
            yield return error!;
            yield break;
        }
        if (payload.Rule is null)
        {
            yield return Invalid("Payload.Rule", "Add-rule payload must include a rule specification.");
            yield break;
        }

        foreach (ApiValidationError result in ValidateRule(payload.Rule, "Payload.Rule"))
        {
            yield return result;
        }
    }

    public static IEnumerable<ApiValidationError> ValidateDelete(JsonElement element)
    {
        if (!TryBind(element, MessageJsonSerializerContext.Default.DeleteRulePayload, s_deletePayloadProperties, out DeleteRulePayload? payload, out ApiValidationError? error))
        {
            yield return error!;
            yield break;
        }
        if (payload.Rule is null || string.IsNullOrWhiteSpace(payload.RuleId))
        {
            yield return Invalid("Payload", "Delete-rule payload must include ruleId and a rule specification.");
            yield break;
        }

        bool validRule = true;
        foreach (ApiValidationError result in ValidateRule(payload.Rule, "Payload.Rule"))
        {
            validRule = false;
            yield return result;
        }
        if (!validRule)
        {
            yield break;
        }

        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(payload.Rule);
        if (normalized.AddressFamily == FirewallAddressFamily.Any)
        {
            yield return Invalid("Payload.Rule.AddressFamily", "Delete-rule specifications must use a concrete address family.");
        }
        if (!RuleIdentity.IsValid(payload.RuleId))
        {
            yield return Invalid("Payload.RuleId", "Rule ID must be a valid semantic rule identity.");
        }
        else if (!string.Equals(RuleIdentity.Compute(payload.Rule), payload.RuleId, StringComparison.Ordinal))
        {
            yield return Invalid("Payload.RuleId", "Rule ID must match the supplied rule specification.");
        }
    }

    public static IEnumerable<ApiValidationError> ValidateBatchDelete(JsonElement element)
    {
        if (!TryBind(element, MessageJsonSerializerContext.Default.BatchDeleteRulesPayload, s_batchDeletePayloadProperties, out BatchDeleteRulesPayload? payload, out ApiValidationError? error))
        {
            yield return error!;
            yield break;
        }
        if (payload.OccurrenceIds is null)
        {
            yield return Invalid("Payload.OccurrenceIds", "Batch-delete payload must include occurrence IDs.");
            yield break;
        }

        ApiValidationError? contractError = ValidateContract(static candidate => RuleBatchDeleteContract.ValidatePayload(candidate), payload);
        if (contractError is not null)
        {
            yield return contractError;
        }
    }

    public static IEnumerable<ApiValidationError> ValidateInsert(JsonElement element)
    {
        if (!TryBind(element, MessageJsonSerializerContext.Default.InsertRulePayload,
                s_insertPayloadProperties, out InsertRulePayload? payload, out ApiValidationError? error))
        {
            yield return error!;
            yield break;
        }
        if (payload.Rule is null)
        {
            yield return Invalid("Payload.Rule", "Insert-rule payload must include a rule specification.");
            yield break;
        }

        bool validRule = true;
        foreach (ApiValidationError result in ValidateRule(payload.Rule, "Payload.Rule"))
        {
            validRule = false;
            yield return result;
        }
        if (!validRule)
        {
            yield break;
        }

        ApiValidationError? contractError = ValidateContract(static candidate => RuleInsertionContract.ValidatePayload(candidate), payload);
        if (contractError is not null)
        {
            yield return contractError;
        }
    }

    public static IEnumerable<ApiValidationError> ValidateReplace(JsonElement element)
    {
        if (!TryBind(element, MessageJsonSerializerContext.Default.ReplaceRulePayload,
                s_replacePayloadProperties, out ReplaceRulePayload? payload, out ApiValidationError? error))
        {
            yield return error!;
            yield break;
        }
        if (payload.ReplacementRule is null)
        {
            yield return Invalid("Payload.ReplacementRule", "Replace-rule payload must include a replacement rule specification.");
            yield break;
        }

        bool validRule = true;
        foreach (ApiValidationError result in ValidateRule(payload.ReplacementRule, "Payload.ReplacementRule"))
        {
            validRule = false;
            yield return result;
        }
        if (!validRule)
        {
            yield break;
        }

        ApiValidationError? contractError = ValidateContract(static candidate => RuleReplacementContract.ValidatePayload(candidate), payload);
        if (contractError is not null)
        {
            yield return contractError;
        }
    }

    public static IEnumerable<ApiValidationError> ValidateReorder(JsonElement element)
    {
        if (!TryBind(element, MessageJsonSerializerContext.Default.ReorderRulesPayload, s_reorderPayloadProperties, out ReorderRulesPayload? payload, out ApiValidationError? error))
        {
            yield return error!;
            yield break;
        }
        if (!FirewallRuleSnapshotFingerprint.IsValid(payload.BaselineFingerprint))
        {
            yield return Invalid("Payload.BaselineFingerprint", "Baseline fingerprint is malformed.");
        }
        if (payload.DesiredOrder is null)
        {
            yield return Invalid("Payload.DesiredOrder", "Desired order is required.");
            yield break;
        }

        HashSet<int> seen = [];
        for (int i = 0; i < payload.DesiredOrder.Length; i++)
        {
            int occurrenceId = payload.DesiredOrder[i];
            if (occurrenceId < 0)
            {
                yield return Invalid("Payload.DesiredOrder", "Desired-order occurrence IDs cannot be negative.");
                yield break;
            }
            if (!seen.Add(occurrenceId))
            {
                yield return Invalid("Payload.DesiredOrder", "Desired-order occurrence IDs must be unique.");
                yield break;
            }
        }
    }

    private static IEnumerable<ApiValidationError> ValidateRule(FirewallRuleSpecification rule, string memberPrefix)
    {
        ModelValidationError[] errors = RuleSpecificationValidator.Validate(rule);
        foreach (ModelValidationError error in errors)
        {
            yield return Invalid($"{memberPrefix}.{error.PropertyName}", error.ErrorMessage, error.Code);
        }
        if (errors.Length != 0)
        {
            yield break;
        }

        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(rule);
        foreach (ApiValidationError result in ValidateCanonicalRule(rule, normalized, memberPrefix))
        {
            yield return result;
        }
    }

    private static IEnumerable<ApiValidationError> ValidateCanonicalRule(FirewallRuleSpecification original, FirewallRuleSpecification normalized, string prefix)
    {
        if (original.Action != normalized.Action)
        {
            yield return NonCanonical(prefix, nameof(original.Action));
        }
        if (original.AddressFamily != normalized.AddressFamily)
        {
            yield return NonCanonical(prefix, nameof(original.AddressFamily));
        }
        if (original.Direction != normalized.Direction)
        {
            yield return NonCanonical(prefix, nameof(original.Direction));
        }
        if (original.Protocol != normalized.Protocol)
        {
            yield return NonCanonical(prefix, nameof(original.Protocol));
        }
        if (!string.Equals(original.Source, normalized.Source, StringComparison.Ordinal))
        {
            yield return NonCanonical(prefix, nameof(original.Source));
        }
        if (!string.Equals(original.SourcePorts, normalized.SourcePorts, StringComparison.Ordinal))
        {
            yield return NonCanonical(prefix, nameof(original.SourcePorts));
        }
        if (!string.Equals(original.SourceInterface, normalized.SourceInterface, StringComparison.Ordinal))
        {
            yield return NonCanonical(prefix, nameof(original.SourceInterface));
        }
        if (!string.Equals(original.Destination, normalized.Destination, StringComparison.Ordinal))
        {
            yield return NonCanonical(prefix, nameof(original.Destination));
        }
        if (!string.Equals(original.DestinationPorts, normalized.DestinationPorts, StringComparison.Ordinal))
        {
            yield return NonCanonical(prefix, nameof(original.DestinationPorts));
        }
        if (!string.Equals(original.DestinationInterface, normalized.DestinationInterface, StringComparison.Ordinal))
        {
            yield return NonCanonical(prefix, nameof(original.DestinationInterface));
        }
        if (!string.Equals(original.Comment, normalized.Comment, StringComparison.Ordinal))
        {
            yield return NonCanonical(prefix, nameof(original.Comment));
        }
    }

    private static ApiValidationError NonCanonical(string prefix, string propertyName) =>
        Invalid($"{prefix}.{propertyName}", "Signed rule payload values must already use their canonical representation.");

    private static ApiValidationError? ValidateContract<TPayload>(Action<TPayload> validate, TPayload payload)
    {
        try
        {
            validate(payload);
            return null;
        }
        catch (ArgumentException exception)
        {
            return Invalid("Payload", exception.Message);
        }
    }

    private static bool TryBind<TPayload>(
        JsonElement element,
        JsonTypeInfo<TPayload> typeInfo,
        IReadOnlyCollection<string> topLevelProperties,
        [NotNullWhen(true)] out TPayload? payload,
        out ApiValidationError? error)
        where TPayload : class
    {
        payload = null;
        if (element.ValueKind != JsonValueKind.Object)
        {
            error = Invalid("Payload", "Payload must be a JSON object.");
            return false;
        }
        if (!HasExactlyExpectedProperties(element, topLevelProperties, topLevelProperties, out string? shapeError))
        {
            error = Invalid("Payload", shapeError!);
            return false;
        }
        if (!ValidateNestedRuleShapes(element, out shapeError))
        {
            error = Invalid("Payload", shapeError!);
            return false;
        }

        try
        {
            payload = JsonSerializer.Deserialize(element, typeInfo)!;
        }
        catch (JsonException)
        {
            error = Invalid("Payload", "Signed intent payload is malformed.");
            return false;
        }
        catch (NotSupportedException)
        {
            error = Invalid("Payload", "Signed intent payload has an unsupported shape.");
            return false;
        }

        if (payload is null)
        {
            error = Invalid("Payload", "Signed intent payload is required.");
            return false;
        }

        error = null;
        return true;
    }

    private static bool ValidateNestedRuleShapes(JsonElement payload, out string? error)
    {
        foreach (string propertyName in s_nestedRuleProperties)
        {
            if (!payload.TryGetProperty(propertyName, out JsonElement rule) || rule.ValueKind == JsonValueKind.Null)
            {
                continue;
            }
            if (rule.ValueKind != JsonValueKind.Object)
            {
                error = $"Payload property '{propertyName}' must be an object.";
                return false;
            }
            if (!HasExactlyExpectedProperties(rule, s_ruleProperties, s_requiredRuleProperties, out error))
            {
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool HasExactlyExpectedProperties(
        JsonElement element,
        IReadOnlyCollection<string> allowed,
        IReadOnlyCollection<string> required,
        out string? error)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                error = $"Duplicate JSON property '{property.Name}' is not supported.";
                return false;
            }
            if (!allowed.Contains(property.Name))
            {
                error = $"Unknown JSON property '{property.Name}' is not supported.";
                return false;
            }
        }

        foreach (string propertyName in required)
        {
            if (!seen.Contains(propertyName))
            {
                error = $"Required JSON property '{propertyName}' is missing.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool IsKeyId(string value)
    {
        if (!value.StartsWith(IntentProtocol.KEY_ID_PREFIX, StringComparison.Ordinal)
            || !IntentSigner.TryDecodeBase64Url(value[IntentProtocol.KEY_ID_PREFIX.Length..], out byte[] digest))
        {
            return false;
        }

        return digest.Length == SHA256.HashSizeInBytes;
    }

    private static ApiValidationError Invalid(string memberName, string message, string? code = null) => new(memberName, code, message);
}
