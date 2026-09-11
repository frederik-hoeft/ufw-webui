using Ufw.Mock.Cli;

namespace Ufw.Mock.Services;

internal sealed class UfwCommandExecutionService
{
    public int Execute(Func<int> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            return operation();
        }
        catch (UfwCliException exception)
        {
            Console.Error.WriteLine("ERROR: " + exception.Message);
            return 1;
        }
    }
}
