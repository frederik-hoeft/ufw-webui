namespace Ufw.Web.Data.Access;

public sealed record DataMutationResult
{
    private DataMutationResult(DataMutationError? error) => Error = error;

    public bool IsSuccess => Error is null;

    public DataMutationError? Error { get; }

    public static DataMutationResult Success() => new(error: null);

    public static DataMutationResult<T> Success<T>(T value) => new(value, error: null);

    public static DataMutationResult<T> Failure<T>(DataMutationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DataMutationResult<T>(default!, error);
    }

    public static DataMutationResult Failure(DataMutationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DataMutationResult(error);
    }
}

public sealed record DataMutationResult<T>
{
    internal DataMutationResult(T value, DataMutationError? error) => (Value, Error) = (value, error);

    public bool IsSuccess => Error is null;

    public T Value { get; }

    public DataMutationError? Error { get; }
}
