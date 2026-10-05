using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Ipc.Model.Responses;

namespace Ufw.Ipc.Client;

/// <summary>
/// Represents the application-level outcome of an IPC request without a response payload.
/// </summary>
/// <remarks>
/// Daemon-declared non-success responses are represented by <see cref="Error"/>. Transport, cancellation, timeout, and malformed-response failures remain exceptional.
/// </remarks>
public sealed class UfwIpcResult
{
    private UfwIpcResult(UfwIpcError? error) => Error = error;

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public UfwIpcError? Error { get; }

    public UfwIpcResult EnsureSuccess()
    {
        if (Error is not null)
        {
            throw new UfwIpcException(Error);
        }
        return this;
    }

    public static UfwIpcResult Success() => new(null);

    public static UfwIpcResult Failure(UfwIpcError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(error);
    }

    internal static UfwIpcResult FromPayloadResult(UfwIpcResult<OkResponse> result) =>
        result.IsSuccess ? Success() : Failure(result.Error);
}

/// <summary>
/// Represents the application-level outcome of an IPC request that returns a response payload on success.
/// </summary>
/// <remarks>
/// Daemon-declared non-success responses are represented by <see cref="Error"/>. Transport, cancellation, timeout, and malformed-response failures remain exceptional.
/// </remarks>
/// <typeparam name="TResponse">The expected success payload type.</typeparam>
public sealed class UfwIpcResult<TResponse> where TResponse : IEquatable<TResponse>
{
    private readonly TResponse? _result;

    private UfwIpcResult(TResponse? result, UfwIpcError? error)
    {
        _result = result;
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public UfwIpcError? Error { get; }

    public TResponse Result
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

    public UfwIpcResult<TResponse> EnsureSuccess()
    {
        _ = Result;
        return this;
    }

    public bool TryGetResult([MaybeNullWhen(false)] out TResponse result, [NotNullWhen(false)] out UfwIpcError? error)
    {
        result = _result!;
        error = Error;
        return error is null;
    }

    public static UfwIpcResult<TResponse> Success(TResponse result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new(result, null);
    }

    public static UfwIpcResult<TResponse> Failure(UfwIpcError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(default, error);
    }
}
