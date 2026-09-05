namespace Ufw.Mock.Cli;

internal sealed class UfwCliException : Exception
{
    public UfwCliException(string message)
        : base(message)
    {
    }
}
