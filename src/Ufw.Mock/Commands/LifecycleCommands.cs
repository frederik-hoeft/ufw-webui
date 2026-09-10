using ConsoleAppFramework;
using Ufw.Mock.Cli;

namespace Ufw.Mock.Commands;

internal sealed class LifecycleCommands(UfwCommandExecutor executor)
{
    public int Enable([Argument] params string[] arguments) => executor.Enable(arguments);

    public int Disable([Argument] params string[] arguments) => executor.Disable(arguments);

    public int Reload([Argument] params string[] arguments) => executor.Reload(arguments);

    public int Reset([Argument] params string[] arguments) => executor.Reset(arguments);
}
