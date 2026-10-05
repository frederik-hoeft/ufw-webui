using System.Diagnostics.CodeAnalysis;
using Ufw.Ipc.Client;

namespace Ufw.Web.Services.Daemon;

/// <summary>
/// Represents the outcome of a daemon operation that does not return a response payload.
/// </summary>
/// <remarks>
/// A daemon-declared non-success response is retained as <see cref="Error"/>. Transport, cancellation, timeout, and malformed-response failures remain exceptional.
/// </remarks>
public sealed class DaemonResult
{
    private DaemonResult(UfwIpcError? error) => Error = error;

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public UfwIpcError? Error { get; }

    public DaemonResult EnsureSuccess()
    {
        if (Error is not null)
        {
            throw new UfwIpcException(Error);
        }
        return this;
    }

    public static DaemonResult Success() => new(null);

    public static DaemonResult<T> Success<T>(T result) where T : class
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DaemonResult<T>(result, null);
    }

    public static DaemonResult Failure(UfwIpcError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(error);
    }

    public static DaemonResult<T> Failure<T>(UfwIpcError error) where T : class
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonResult<T>(null, error);
    }

    internal static async Task<DaemonResult> FromIpcAsync(Func<Task<UfwIpcResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            UfwIpcResult result = await operation();
            return result.IsSuccess ? Success() : Failure(result.Error);
        }
        catch (UfwIpcInvalidResponseException exception)
        {
            throw new DaemonInvalidResponseException(exception.Message, exception);
        }
    }

    internal static async Task<DaemonResult<T>> FromIpcAsync<T>(Func<Task<UfwIpcResult<T>>> operation) where T : class, IEquatable<T>
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            UfwIpcResult<T> result = await operation();
            if (result.TryGetResult(out T? response, out UfwIpcError? error))
            {
                return Success(response);
            }
            return Failure<T>(error);
        }
        catch (UfwIpcInvalidResponseException exception)
        {
            throw new DaemonInvalidResponseException(exception.Message, exception);
        }
    }
}

/// <summary>
/// Represents the outcome of a daemon operation that returns a response payload on success.
/// </summary>
/// <remarks>
/// A daemon-declared non-success response is retained as <see cref="Error"/>. Transport, cancellation, timeout, and malformed-response failures remain exceptional.
/// </remarks>
/// <typeparam name="T">The successful response payload type.</typeparam>
public sealed class DaemonResult<T> where T : class
{
    private readonly T? _result;

    internal DaemonResult(T? result, UfwIpcError? error)
    {
        _result = result;
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public UfwIpcError? Error { get; }

    public T Result
    {
        get
        {
            if (Error is not null)
            {
                throw new UfwIpcException(Error);
            }
            return _result!;
        }
    }

    public DaemonResult<T> EnsureSuccess()
    {
        _ = Result;
        return this;
    }

    public bool TryGetResult([NotNullWhen(true)] out T? result, [NotNullWhen(false)] out UfwIpcError? error)
    {
        result = _result;
        error = Error;
        return error is null;
    }
}
