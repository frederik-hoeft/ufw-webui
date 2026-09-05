using ConsoleAppFramework;
using Ufw.Mock.Cli;

namespace Ufw.Mock.Commands;

internal static class CommandRuntime
{
    public static UfwCommandExecutor Create(ConsoleAppContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.GlobalOptions is not UfwGlobalOptions options)
        {
            throw new InvalidOperationException("UFW global options were not configured.");
        }
        return new UfwCommandExecutor(options);
    }
}
