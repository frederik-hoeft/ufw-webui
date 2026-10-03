using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
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
    private DaemonResult(UfwIpcException? error) => Error = error;

    /// <summary>
    /// Gets whether the daemon operation completed successfully.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>
    /// Gets the daemon application error when the operation did not complete successfully.
    /// </summary>
    public UfwIpcException? Error { get; }

    /// <summary>
    /// Throws the daemon application error when the operation did not complete successfully.
    /// </summary>
    /// <returns>This result.</returns>
    public DaemonResult EnsureSuccess()
    {
        if (Error is not null)
        {
            ExceptionDispatchInfo.Capture(Error).Throw();
        }
        return this;
    }

    public static DaemonResult Success() => new(null);

    public static DaemonResult<T> Success<T>(T result) where T : class
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DaemonResult<T>(result, null);
    }

    public static DaemonResult Failure(UfwIpcException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(error);
    }

    public static DaemonResult<T> Failure<T>(UfwIpcException error) where T : class
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonResult<T>(null, error);
    }

    internal static async Task<DaemonResult> CaptureAsync(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            await operation();
            return Success();
        }
        catch (UfwIpcException exception)
        {
            return Failure(exception);
        }
        catch (InvalidDataException exception)
        {
            throw new DaemonInvalidResponseException(exception.Message, exception);
        }
    }

    internal static async Task<DaemonResult<T>> CaptureAsync<T>(Func<Task<T>> operation) where T : class
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            return Success(await operation());
        }
        catch (UfwIpcException exception)
        {
            return Failure<T>(exception);
        }
        catch (InvalidDataException exception)
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

    internal DaemonResult(T? result, UfwIpcException? error)
    {
        _result = result;
        Error = error;
    }

    /// <summary>
    /// Gets whether the daemon operation completed successfully.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    /// <summary>
    /// Gets the daemon application error when the operation did not complete successfully.
    /// </summary>
    public UfwIpcException? Error { get; }

    /// <summary>
    /// Gets the successful response payload, throwing the daemon application error when the operation failed.
    /// </summary>
    public T Result
    {
        get
        {
            if (Error is not null)
            {
                ExceptionDispatchInfo.Capture(Error).Throw();
            }
            return _result!;
        }
    }

    /// <summary>
    /// Throws the daemon application error when the operation failed.
    /// </summary>
    /// <returns>This result.</returns>
    public DaemonResult<T> EnsureSuccess()
    {
        _ = Result;
        return this;
    }

    /// <summary>
    /// Attempts to obtain the successful response without throwing for a daemon application error.
    /// </summary>
    public bool TryGetResult([NotNullWhen(true)] out T? result, [NotNullWhen(false)] out UfwIpcException? error)
    {
        result = _result;
        error = Error;
        return error is null;
    }
}
