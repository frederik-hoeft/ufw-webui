using Microsoft.JSInterop;
using Ufw.Client.Errors;

namespace Ufw.Client.Intent;

internal sealed partial class BrowserIntentCryptoService(IJSRuntime jsRuntime, ILogger<BrowserIntentCryptoService> logger) : IBrowserIntentCryptoService, IAsyncDisposable
{
    private const string MODULE_PATH = "./js/intentSigning.js";
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private IJSObjectReference? _module;
    private int _disposeState;

    public Task<string> GetKeyIdAsync(string privateKey, CancellationToken cancellationToken = default) =>
        InvokeAsync(module => module.InvokeAsync<string>("getKeyId", cancellationToken, privateKey).AsTask(), cancellationToken);

    public Task<string> CreateNonceAsync(int sizeBytes, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);
        return InvokeAsync(module => module.InvokeAsync<string>("createNonce", cancellationToken, sizeBytes).AsTask(), cancellationToken);
    }

    public Task<string> SignAsync(string privateKey, byte[] payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return InvokeAsync(module => module.InvokeAsync<string>("sign", cancellationToken, privateKey, payload).AsTask(), cancellationToken);
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

    private async Task<T> InvokeAsync<T>(Func<IJSObjectReference, Task<T>> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
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
            throw new BrowserOperationException("The browser could not load the intent-signing module.", exception);
        }
    }
}
