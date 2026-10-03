namespace Ufw.Web.Data.Access;

public sealed record DataMutationResult
{
    private DataMutationResult(DataMutationError? error) => Error = error;

    public bool IsSuccess => Error is null;

    public DataMutationError? Error { get; }

    public static DataMutationResult Success() => new(error: null);

    public static DataMutationResult Failure(DataMutationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DataMutationResult(error);
    }
}
