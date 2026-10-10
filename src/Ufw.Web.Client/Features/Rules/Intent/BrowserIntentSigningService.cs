using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Model.V1.Rules.Intent;

namespace Ufw.Web.Client.Features.Rules.Intent;

internal sealed class BrowserIntentSigningService(IBrowserIntentCryptoService crypto, TimeProvider timeProvider) : IIntentSigningService
{
    public Task<AddRuleIntentRequest> CreateAddRuleRequestAsync(string deploymentId, FirewallRuleSpecification rule, string privateKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        ArgumentNullException.ThrowIfNull(rule);
        ValidatePrivateKey(privateKey);
        AddRulePayload payload = new() { Rule = RuleSpecificationNormalizer.Normalize(rule) };
        return SignAsync(
            deploymentId,
            privateKey,
            IntentOperations.ADD_RULE,
            payload,
            MessageJsonSerializerContext.Default.AddRulePayload,
            IntentCanonicalizer.CanonicalizeAdd,
            static envelope => new AddRuleIntentRequest
            {
                Version = IntentProtocol.VERSION,
                DeploymentId = envelope.DeploymentId,
                KeyId = envelope.KeyId,
                IssuedAtUnix = envelope.IssuedAtUnix,
                Nonce = envelope.Nonce,
                Operation = envelope.Operation,
                Payload = envelope.Payload,
                Signature = string.Empty,
            },
            cancellationToken);
    }

    public Task<DeleteRuleIntentRequest> CreateDeleteRuleRequestAsync(string deploymentId, string ruleId, FirewallRuleSpecification rule, string privateKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentNullException.ThrowIfNull(rule);
        ValidatePrivateKey(privateKey);
        DeleteRulePayload payload = new()
        {
            RuleId = ruleId,
            Rule = RuleSpecificationNormalizer.Normalize(rule),
        };
        return SignAsync(
            deploymentId,
            privateKey,
            IntentOperations.DELETE_RULE,
            payload,
            MessageJsonSerializerContext.Default.DeleteRulePayload,
            IntentCanonicalizer.CanonicalizeDelete,
            static envelope => new DeleteRuleIntentRequest
            {
                Version = IntentProtocol.VERSION,
                DeploymentId = envelope.DeploymentId,
                KeyId = envelope.KeyId,
                IssuedAtUnix = envelope.IssuedAtUnix,
                Nonce = envelope.Nonce,
                Operation = envelope.Operation,
                Payload = envelope.Payload,
                Signature = string.Empty,
            },
            cancellationToken);
    }

    public Task<BatchDeleteRulesIntentRequest> CreateBatchDeleteRulesRequestAsync(string deploymentId, string baselineFingerprint, IReadOnlyList<int> occurrenceIds, string privateKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        ArgumentNullException.ThrowIfNull(occurrenceIds);
        ValidatePrivateKey(privateKey);
        BatchDeleteRulesPayload payload = new()
        {
            BaselineFingerprint = baselineFingerprint,
            OccurrenceIds = [.. occurrenceIds],
        };
        RuleBatchDeleteContract.ValidatePayload(payload);
        return SignAsync(
            deploymentId,
            privateKey,
            IntentOperations.DELETE_RULES_BATCH,
            payload,
            MessageJsonSerializerContext.Default.BatchDeleteRulesPayload,
            IntentCanonicalizer.CanonicalizeBatchDelete,
            static envelope => new BatchDeleteRulesIntentRequest
            {
                Version = IntentProtocol.VERSION,
                DeploymentId = envelope.DeploymentId,
                KeyId = envelope.KeyId,
                IssuedAtUnix = envelope.IssuedAtUnix,
                Nonce = envelope.Nonce,
                Operation = envelope.Operation,
                Payload = envelope.Payload,
                Signature = string.Empty,
            },
            cancellationToken);
    }

    public Task<InsertRuleIntentRequest> CreateInsertRuleRequestAsync(string deploymentId, string baselineFingerprint, int anchorOccurrenceId, RuleInsertionPlacement placement, FirewallRuleSpecification rule, string privateKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        ArgumentNullException.ThrowIfNull(rule);
        ValidatePrivateKey(privateKey);
        InsertRulePayload payload = new()
        {
            BaselineFingerprint = baselineFingerprint,
            AnchorOccurrenceId = anchorOccurrenceId,
            Placement = placement,
            Rule = RuleSpecificationNormalizer.Normalize(rule),
        };
        RuleInsertionContract.ValidatePayload(payload);
        return SignAsync(
            deploymentId,
            privateKey,
            IntentOperations.INSERT_RULE,
            payload,
            MessageJsonSerializerContext.Default.InsertRulePayload,
            IntentCanonicalizer.CanonicalizeInsert,
            static envelope => new InsertRuleIntentRequest
            {
                Version = IntentProtocol.VERSION,
                DeploymentId = envelope.DeploymentId,
                KeyId = envelope.KeyId,
                IssuedAtUnix = envelope.IssuedAtUnix,
                Nonce = envelope.Nonce,
                Operation = envelope.Operation,
                Payload = envelope.Payload,
                Signature = string.Empty,
            },
            cancellationToken);
    }

    public Task<ReplaceRuleIntentRequest> CreateReplaceRuleRequestAsync(string deploymentId, string baselineFingerprint, int targetOccurrenceId, string originalRuleId, FirewallRuleSpecification replacementRule, string privateKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        ArgumentNullException.ThrowIfNull(replacementRule);
        ValidatePrivateKey(privateKey);
        ReplaceRulePayload payload = new()
        {
            BaselineFingerprint = baselineFingerprint,
            TargetOccurrenceId = targetOccurrenceId,
            OriginalRuleId = originalRuleId,
            ReplacementRule = RuleSpecificationNormalizer.Normalize(replacementRule),
        };
        RuleReplacementContract.ValidatePayload(payload);
        return SignAsync(
            deploymentId,
            privateKey,
            IntentOperations.REPLACE_RULE,
            payload,
            MessageJsonSerializerContext.Default.ReplaceRulePayload,
            IntentCanonicalizer.CanonicalizeReplace,
            static envelope => new ReplaceRuleIntentRequest
            {
                Version = IntentProtocol.VERSION,
                DeploymentId = envelope.DeploymentId,
                KeyId = envelope.KeyId,
                IssuedAtUnix = envelope.IssuedAtUnix,
                Nonce = envelope.Nonce,
                Operation = envelope.Operation,
                Payload = envelope.Payload,
                Signature = string.Empty,
            },
            cancellationToken);
    }

    public Task<ReorderRulesIntentRequest> CreateReorderRulesRequestAsync(string deploymentId, string baselineFingerprint, IReadOnlyList<int> desiredOrder, string privateKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        if (!FirewallRuleSnapshotFingerprint.IsValid(baselineFingerprint))
        {
            throw new ArgumentException("A valid firewall snapshot fingerprint is required.", nameof(baselineFingerprint));
        }
        ArgumentNullException.ThrowIfNull(desiredOrder);
        ValidatePrivateKey(privateKey);
        ReorderRulesPayload payload = new()
        {
            BaselineFingerprint = baselineFingerprint,
            DesiredOrder = [.. desiredOrder],
        };
        return SignAsync(
            deploymentId,
            privateKey,
            IntentOperations.REORDER_RULES,
            payload,
            MessageJsonSerializerContext.Default.ReorderRulesPayload,
            IntentCanonicalizer.CanonicalizeReorder,
            static envelope => new ReorderRulesIntentRequest
            {
                Version = IntentProtocol.VERSION,
                DeploymentId = envelope.DeploymentId,
                KeyId = envelope.KeyId,
                IssuedAtUnix = envelope.IssuedAtUnix,
                Nonce = envelope.Nonce,
                Operation = envelope.Operation,
                Payload = envelope.Payload,
                Signature = string.Empty,
            },
            cancellationToken);
    }

    private async Task<TRequest> SignAsync<TRequest, TPayload>(
        string deploymentId,
        string privateKey,
        string operation,
        TPayload payload,
        JsonTypeInfo<TPayload> payloadTypeInfo,
        Func<ISignedIntent, TPayload, byte[]> canonicalize,
        Func<UnsignedEnvelope, TRequest> createRequest,
        CancellationToken cancellationToken)
        where TRequest : SignedRuleIntentRequest
    {
        string keyId = await crypto.GetKeyIdAsync(privateKey, cancellationToken);
        string nonce = await crypto.CreateNonceAsync(IntentProtocol.NONCE_SIZE_BYTES, cancellationToken);
        UnsignedEnvelope envelope = new(
            deploymentId,
            keyId,
            timeProvider.GetUtcNow().ToUnixTimeSeconds(),
            nonce,
            operation,
            JsonSerializer.SerializeToElement(payload, payloadTypeInfo));

        TRequest unsignedRequest = createRequest(envelope);
        byte[] canonical = canonicalize(unsignedRequest, payload);
        string signature = await crypto.SignAsync(privateKey, canonical, cancellationToken);
        return (TRequest)((SignedRuleIntentRequest)unsignedRequest with { Signature = signature });
    }

    private static void ValidatePrivateKey(string privateKey)
    {
        if (string.IsNullOrWhiteSpace(privateKey))
        {
            throw new ArgumentException("A private key is required for this request.", nameof(privateKey));
        }
    }

    private sealed record UnsignedEnvelope(string DeploymentId, string KeyId, long IssuedAtUnix, string Nonce, string Operation, JsonElement Payload);
}
