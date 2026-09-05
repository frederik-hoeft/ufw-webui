using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Ufw.Client.Errors;

namespace Ufw.Client.Auth;

internal sealed partial class BrowserAuthenticationOperationCoordinator(
    IJSRuntime jsRuntime,
    ILogger<BrowserAuthenticationOperationCoordinator> logger) : IAuthenticationOperationCoordinator, IAsyncDisposable
{
    private const string LOCK_NAME = "ufw-webui-auth-session";
    private const string MODULE_PATH = "./js/authCoordination.js";
    private readonly SemaphoreSlim _localLock = new(1, 1);
    private IJSObjectReference? _module;
    private int _disposeState;

    public async Task RunExclusiveAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await RunExclusiveAsync(
            async operationCancellationToken =>
            {
                await operation(operationCancellationToken);
                return true;
            },
            cancellationToken);
    }

    public async Task<T> RunExclusiveAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);

        await _localLock.WaitAsync(cancellationToken);
        string requestId = Guid.NewGuid().ToString("N");
        IJSObjectReference? module = null;
        bool browserLockAcquired = false;
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);
            module = await GetModuleAsync(cancellationToken);
            await AcquireBrowserLockAsync(module, requestId, cancellationToken);
            browserLockAcquired = true;
            return await operation(cancellationToken);
        }
        finally
        {
            if (browserLockAcquired && module is not null)
            {
                await ReleaseBrowserLockAsync(module, requestId);
            }

            _localLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        await _localLock.WaitAsync();
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
            _localLock.Release();
            _localLock.Dispose();
        }
    }

    private async Task<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken)
    {
        if (_module is not null)
        {
            return _module;
        }

        _module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, MODULE_PATH);
        return _module;
    }

    private static async Task AcquireBrowserLockAsync(
        IJSObjectReference module,
        string requestId,
        CancellationToken cancellationToken)
    {
        Task acquisition = module.InvokeVoidAsync(
            "acquire",
            CancellationToken.None,
            LOCK_NAME,
            requestId).AsTask();

        try
        {
            await acquisition.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            try
            {
                await module.InvokeVoidAsync("cancelAndRelease", CancellationToken.None, requestId);
                await acquisition;
            }
            catch (Exception exception) when (exception is JSException or JSDisconnectedException)
            {
                // Aborting a queued Web Lock rejects its request promise with AbortError.
            }

            throw;
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException)
        {
            throw new BrowserOperationException(
                "The browser could not coordinate authentication state across tabs.",
                exception);
        }
    }

    [LoggerMessage(LogLevel.Debug, "Could not dispose the browser authentication coordination module.")]
    private static partial void LogModuleDisposeFailure(ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Warning, "Could not release the browser authentication coordination lock.")]
    private static partial void LogLockReleaseFailure(ILogger logger, Exception exception);

    private async Task ReleaseBrowserLockAsync(IJSObjectReference module, string requestId)
    {
        try
        {
            await module.InvokeVoidAsync("release", CancellationToken.None, requestId);
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException)
        {
            LogLockReleaseFailure(logger, exception);
        }
    }
}
