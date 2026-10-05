namespace Ufw.Ipc.Client;

/// <summary>
/// Raised when the daemon returns an application response that cannot be interpreted according to the IPC response contract.
/// </summary>
public sealed class UfwIpcInvalidResponseException : Exception
{
    public UfwIpcInvalidResponseException()
    {
    }

    public UfwIpcInvalidResponseException(string? message) : base(message)
    {
    }

    public UfwIpcInvalidResponseException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
