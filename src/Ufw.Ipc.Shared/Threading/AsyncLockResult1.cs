using System.Diagnostics.CodeAnalysis;

namespace Ufw.Ipc.Shared.Threading;

public readonly record struct AsyncLockResult<TResult>(TResult? Result, bool TaskExecuted)
{
    public static implicit operator AsyncLockResult(AsyncLockResult<TResult> result) => new(result.TaskExecuted);

    public bool TryGetResult([MaybeNullWhen(false)] out TResult result)
    {
        result = Result;
        return TaskExecuted;
    }
}
