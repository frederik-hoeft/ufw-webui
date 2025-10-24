using System.Runtime.CompilerServices;

namespace Ufw.Pipes.Shared;

internal static class AwaitableExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ConfiguredTaskAwaitable NoCapture(this Task task) => task.ConfigureAwait(false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ConfiguredValueTaskAwaitable NoCapture(this ValueTask task) => task.ConfigureAwait(false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ConfiguredTaskAwaitable<TResult> NoCapture<TResult>(this Task<TResult> task) => task.ConfigureAwait(false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ConfiguredValueTaskAwaitable<TResult> NoCapture<TResult>(this ValueTask<TResult> task) => task.ConfigureAwait(false);
}
