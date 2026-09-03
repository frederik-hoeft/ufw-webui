using Ufw.Ipc.Shared.Threading;

namespace Ufw.Systemd.Firewall;

internal sealed class UfwExecutionGate : IUfwExecutionGate, IDisposable
{
    private readonly AsyncLock _lock = new();
    private bool _disposed;

    public Task<TResult> RunAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _lock.RunTaskAsync(action, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lock.Dispose();
    }
}
