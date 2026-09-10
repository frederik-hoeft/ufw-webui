using ConsoleAppFramework;
using Ufw.Mock.Cli;

namespace Ufw.Mock.Commands;

internal sealed class StatusCommands(UfwCommandExecutor executor)
{
    public int Status([Argument] params string[] arguments) =>
        executor.Status(arguments);

    public int Show([Argument] params string[] arguments) =>
        executor.Show(arguments);
}
