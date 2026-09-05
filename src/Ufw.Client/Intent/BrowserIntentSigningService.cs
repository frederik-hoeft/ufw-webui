using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Ufw.Client.Errors;
using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Ipc.Shared.Model.Requests.Domain;
using Ufw.Ipc.Shared.Security.Intent;
using Ufw.Ipc.Shared.Serialization.Json;

namespace Ufw.Client.Intent;

internal sealed partial class BrowserIntentSigningService(
    IJSRuntime jsRuntime,
    TimeProvider timeProvider,
    ILogger<BrowserIntentSigningService> logger) : IIntentSigningService, IAsyncDisposable
{
    private const string MODULE_PATH = "./js/intentSigning.js";
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private IJSObjectReference? _module;
    private int _disposeState;

    public Task<AddRuleRequest> CreateAddRuleRequestAsync(
        string deploymentId,
        FirewallRuleSpecification rule,
        string privateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentId);
        ArgumentNullException.ThrowIfNull(rule);
        ValidatePrivateKey(privateKey);

        return RunWithModuleAsync(
            async module =>
            {
                string keyId = await module.InvokeAsync<string>("getKeyId", cancellationToken, privateKey);
                string nonce = await module.InvokeAsync<string>("createNonce", cancellationToken, IntentProtocol.NONCE_SIZE_BYTES);
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
                string signature = await module.InvokeAsync<string>("sign", cancellationToken, privateKey, canonical);
                return unsignedRequest with { Signature = signature };
            },
            cancellationToken);
    }

    public Task<DeleteRuleRequest> CreateDeleteRuleRequestAsync(
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

        return RunWithModuleAsync(
            async module =>
            {
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
                    IssuedAtUnix = timeProvider.GetUtcNow().ToUnixTimeSeconds(),
                    Nonce = nonce,
                    Operation = IntentOperations.DELETE_RULE,
                    Payload = JsonSerializer.SerializeToElement(payload, MessageJsonSerializerContext.Default.DeleteRulePayload),
                    Signature = string.Empty,
                };

                byte[] canonical = IntentCanonicalizer.CanonicalizeDelete(unsignedRequest, payload);
                string signature = await module.InvokeAsync<string>("sign", cancellationToken, privateKey, canonical);
                return unsignedRequest with { Signature = signature };
            },
            cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        await _operationLock.WaitAsync();
        try
        {
            if (_module is not null)
            {
                try
                {
                    await _module.DisposeAsync();
                }
                catch (Exception exception) when (exception is JSException or JSDisconnectedException)
                {
                    LogModuleDisposeFailure(logger, exception);
                }
                finally
                {
                    _module = null;
                }
            }
        }
        finally
        {
            _operationLock.Release();
            _operationLock.Dispose();
        }
    }

    [LoggerMessage(LogLevel.Debug, "Could not dispose the browser intent-signing module.")]
    private static partial void LogModuleDisposeFailure(ILogger logger, Exception exception);

    private async Task<T> RunWithModuleAsync<T>(
        Func<IJSObjectReference, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);
            IJSObjectReference module = await GetOrImportModuleAsync(cancellationToken);
            return await operation(module);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task<IJSObjectReference> GetOrImportModuleAsync(CancellationToken cancellationToken)
    {
        if (_module is not null)
        {
            return _module;
        }

        try
        {
            _module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, MODULE_PATH);
            return _module;
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException)
        {
            throw new BrowserOperationException(
                "The browser could not load the intent-signing module.",
                exception);
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
