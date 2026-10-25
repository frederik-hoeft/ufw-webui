namespace Ufw.Mock.Cli;

public sealed class UfwCliException : Exception
{
    public UfwCliException()
    {
    }

    public UfwCliException(string message) : base(message)
    {
    }

    public UfwCliException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
