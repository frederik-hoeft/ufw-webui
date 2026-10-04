namespace Ufw.Web.Services.Daemon;

/// <summary>
/// Represents a daemon response that was successfully received but violates the Web application's expected response contract.
/// </summary>
internal sealed class DaemonInvalidResponseException : InvalidOperationException
{
    public DaemonInvalidResponseException(string message) : base(message)
    {
    }

    public DaemonInvalidResponseException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
