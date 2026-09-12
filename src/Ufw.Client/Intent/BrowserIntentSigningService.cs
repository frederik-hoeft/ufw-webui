using System.Text.Json;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;

namespace Ufw.Client.Intent;

internal sealed class BrowserIntentSigningService(IBrowserIntentCryptoService crypto, TimeProvider timeProvider) : IIntentSigningService
{
    public async Task<AddRuleRequest> CreateAddRuleRequestAsync(string deploymentId, FirewallRuleSpecification rule, string privateKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        ArgumentNullException.ThrowIfNull(rule);
        ValidatePrivateKey(privateKey);

        string keyId = await crypto.GetKeyIdAsync(privateKey, cancellationToken);
        string nonce = await crypto.CreateNonceAsync(IntentProtocol.NONCE_SIZE_BYTES, cancellationToken);
        AddRulePayload payload = new() { Rule = RuleSpecificationNormalizer.Normalize(rule) };
        AddRuleRequest unsignedRequest = new()
        {
            DeploymentId = deploymentId,
            KeyId = keyId,
            IssuedAtUnix = timeProvider.GetUtcNow().ToUnixTimeSeconds(),
            Nonce = nonce,
            Operation = IntentOperations.ADD_RULE,
            Payload = JsonSerializer.SerializeToElement(payload, MessageJsonSerializerContext.Default.AddRulePayload),
            Signature = string.Empty,
        };

        byte[] canonical = IntentCanonicalizer.CanonicalizeAdd(unsignedRequest, payload);
        string signature = await crypto.SignAsync(privateKey, canonical, cancellationToken);
        return unsignedRequest with { Signature = signature };
    }

    public async Task<DeleteRuleRequest> CreateDeleteRuleRequestAsync(
        string deploymentId,
        string ruleId,
        FirewallRuleSpecification rule,
        string privateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentNullException.ThrowIfNull(rule);
        ValidatePrivateKey(privateKey);

        string keyId = await crypto.GetKeyIdAsync(privateKey, cancellationToken);
        string nonce = await crypto.CreateNonceAsync(IntentProtocol.NONCE_SIZE_BYTES, cancellationToken);
        DeleteRulePayload payload = new()
        {
            RuleId = ruleId,
            Rule = RuleSpecificationNormalizer.Normalize(rule),
        };
        DeleteRuleRequest unsignedRequest = new()
        {
            DeploymentId = deploymentId,
            KeyId = keyId,
            IssuedAtUnix = timeProvider.GetUtcNow().ToUnixTimeSeconds(),
            Nonce = nonce,
            Operation = IntentOperations.DELETE_RULE,
            Payload = JsonSerializer.SerializeToElement(payload, MessageJsonSerializerContext.Default.DeleteRulePayload),
            Signature = string.Empty,
        };

        byte[] canonical = IntentCanonicalizer.CanonicalizeDelete(unsignedRequest, payload);
        string signature = await crypto.SignAsync(privateKey, canonical, cancellationToken);
        return unsignedRequest with { Signature = signature };
    }

    public async Task<ReorderRulesRequest> CreateReorderRulesRequestAsync(
        string deploymentId,
        string baselineFingerprint,
        IReadOnlyList<int> desiredOrder,
        string privateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        if (!FirewallRuleSnapshotFingerprint.IsValid(baselineFingerprint))
        {
            throw new ArgumentException("A valid firewall snapshot fingerprint is required.", nameof(baselineFingerprint));
        }

        ArgumentNullException.ThrowIfNull(desiredOrder);
        ValidatePrivateKey(privateKey);

        string keyId = await crypto.GetKeyIdAsync(privateKey, cancellationToken);
        string nonce = await crypto.CreateNonceAsync(IntentProtocol.NONCE_SIZE_BYTES, cancellationToken);
        ReorderRulesPayload payload = new()
        {
            BaselineFingerprint = baselineFingerprint,
            DesiredOrder = [.. desiredOrder],
        };
        ReorderRulesRequest unsignedRequest = new()
        {
            DeploymentId = deploymentId,
            KeyId = keyId,
            IssuedAtUnix = timeProvider.GetUtcNow().ToUnixTimeSeconds(),
            Nonce = nonce,
            Operation = IntentOperations.REORDER_RULES,
            Payload = JsonSerializer.SerializeToElement(payload, MessageJsonSerializerContext.Default.ReorderRulesPayload),
            Signature = string.Empty,
        };

        byte[] canonical = IntentCanonicalizer.CanonicalizeReorder(unsignedRequest, payload);
        string signature = await crypto.SignAsync(privateKey, canonical, cancellationToken);
        return unsignedRequest with { Signature = signature };
    }

    private static void ValidatePrivateKey(string privateKey)
    {
        if (string.IsNullOrWhiteSpace(privateKey))
        {
            throw new ArgumentException("A private key is required for this request.", nameof(privateKey));
        }
    }
}
