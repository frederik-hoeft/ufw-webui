using ConsoleAppFramework;
using Ufw.Mock.Cli;

namespace Ufw.Mock.Commands;

internal sealed class PolicyCommands(UfwCommandExecutor executor)
{
    public int Default([Argument] params string[] arguments) =>
        executor.SetDefault(arguments);

    public int Logging([Argument] params string[] arguments) =>
        executor.SetLogging(arguments);
}
