using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Ufw.Ipc.Shared.Model.Requests.Domain;

namespace Ufw.Ipc.Shared.Security.Intent;

/// <summary>
/// Builds signed mutation requests. Intended for tests and a future browser/client
/// that holds the user's EC private key.
/// </summary>
public static class IntentRequestFactory
{
    public static AddRuleRequest CreateAddRequest(
        ECDsa privateKey,
        string deploymentId,
        AddRulePayload payload,
        JsonTypeInfo<AddRulePayload> payloadTypeInfo,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(payloadTypeInfo);
        ArgumentNullException.ThrowIfNull(timeProvider);

        AddRuleRequest unsigned = new()
        {
            Version = IntentProtocol.VERSION,
            DeploymentId = deploymentId,
            KeyId = IntentSigner.ComputeKeyId(privateKey),
            IssuedAtUnix = timeProvider.GetUtcNow().ToUnixTimeSeconds(),
            Nonce = IntentSigner.CreateNonce(),
            Operation = IntentOperations.ADD_RULE,
            Payload = JsonSerializer.SerializeToElement(payload, payloadTypeInfo),
            Signature = string.Empty,
        };
        byte[] canonical = IntentCanonicalizer.CanonicalizeAdd(unsigned, payload);
        return unsigned with { Signature = IntentSigner.Sign(privateKey, canonical) };
    }

    public static DeleteRuleRequest CreateDeleteRequest(
        ECDsa privateKey,
        string deploymentId,
        DeleteRulePayload payload,
        JsonTypeInfo<DeleteRulePayload> payloadTypeInfo,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(payloadTypeInfo);
        ArgumentNullException.ThrowIfNull(timeProvider);

        DeleteRuleRequest unsigned = new()
        {
            Version = IntentProtocol.VERSION,
            DeploymentId = deploymentId,
            KeyId = IntentSigner.ComputeKeyId(privateKey),
            IssuedAtUnix = timeProvider.GetUtcNow().ToUnixTimeSeconds(),
            Nonce = IntentSigner.CreateNonce(),
            Operation = IntentOperations.DELETE_RULE,
            Payload = JsonSerializer.SerializeToElement(payload, payloadTypeInfo),
            Signature = string.Empty,
        };
        byte[] canonical = IntentCanonicalizer.CanonicalizeDelete(unsigned, payload);
        return unsigned with { Signature = IntentSigner.Sign(privateKey, canonical) };
    }
}
