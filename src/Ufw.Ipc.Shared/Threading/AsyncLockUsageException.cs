namespace Ufw.Ipc.Shared.Threading;

/// <summary>
/// The exception that is thrown when an async flow violates <see cref="AsyncLock"/> reentrancy requirements.
/// </summary>
public sealed class AsyncLockUsageException : InvalidOperationException
{
    public AsyncLockUsageException() { }

    public AsyncLockUsageException(string? message) : base(message) { }

    public AsyncLockUsageException(string? message, Exception? innerException) : base(message, innerException) { }
}
