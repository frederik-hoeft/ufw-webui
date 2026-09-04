using System.Text.Json;
using Microsoft.JSInterop;
using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Ipc.Shared.Model.Requests.Domain;
using Ufw.Ipc.Shared.Security.Intent;
using Ufw.Ipc.Shared.Serialization.Json;

namespace Ufw.Client.Intent;

internal sealed class BrowserIntentSigningService(IJSRuntime jsRuntime) : IIntentSigningService, IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _module = new(() =>
        jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/intentSigning.js").AsTask());

    public async Task<AddRuleRequest> CreateAddRuleRequestAsync(
        string deploymentId,
        FirewallRuleSpecification rule,
        string privateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        ArgumentNullException.ThrowIfNull(rule);
        ValidatePrivateKey(privateKey);

        IJSObjectReference module = await _module.Value;
        string keyId = await module.InvokeAsync<string>("getKeyId", cancellationToken, privateKey);
        string nonce = await module.InvokeAsync<string>("createNonce", cancellationToken, IntentProtocol.NONCE_SIZE_BYTES);
        AddRulePayload payload = new() { Rule = RuleSpecificationNormalizer.Normalize(rule) };

        AddRuleRequest unsignedRequest = new()
        {
            DeploymentId = deploymentId,
            KeyId = keyId,
            IssuedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Nonce = nonce,
            Operation = IntentOperations.ADD_RULE,
            Payload = JsonSerializer.SerializeToElement(payload, MessageJsonSerializerContext.Default.AddRulePayload),
            Signature = string.Empty,
        };

        byte[] canonical = IntentCanonicalizer.CanonicalizeAdd(unsignedRequest, payload);
        string signature = await module.InvokeAsync<string>("sign", cancellationToken, privateKey, canonical);
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

        IJSObjectReference module = await _module.Value;
        string keyId = await module.InvokeAsync<string>("getKeyId", cancellationToken, privateKey);
        string nonce = await module.InvokeAsync<string>("createNonce", cancellationToken, IntentProtocol.NONCE_SIZE_BYTES);
        DeleteRulePayload payload = new()
        {
            RuleId = ruleId,
            Rule = RuleSpecificationNormalizer.Normalize(rule),
        };

        DeleteRuleRequest unsignedRequest = new()
        {
            DeploymentId = deploymentId,
            KeyId = keyId,
            IssuedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Nonce = nonce,
            Operation = IntentOperations.DELETE_RULE,
            Payload = JsonSerializer.SerializeToElement(payload, MessageJsonSerializerContext.Default.DeleteRulePayload),
            Signature = string.Empty,
        };

        byte[] canonical = IntentCanonicalizer.CanonicalizeDelete(unsignedRequest, payload);
        string signature = await module.InvokeAsync<string>("sign", cancellationToken, privateKey, canonical);
        return unsignedRequest with { Signature = signature };
    }

    public async ValueTask DisposeAsync()
    {
        if (_module.IsValueCreated)
        {
            IJSObjectReference module = await _module.Value;
            await module.DisposeAsync();
        }
    }

    private static void ValidatePrivateKey(string privateKey)
    {
        if (string.IsNullOrWhiteSpace(privateKey))
        {
            throw new ArgumentException("A private key is required for this request.", nameof(privateKey));
        }
    }
}
