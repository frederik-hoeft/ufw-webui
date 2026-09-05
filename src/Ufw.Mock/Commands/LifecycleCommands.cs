using ConsoleAppFramework;

namespace Ufw.Mock.Commands;

internal sealed class LifecycleCommands
{
    public int Enable(ConsoleAppContext context, [Argument] params string[] arguments) => CommandRuntime.Create(context).Enable(arguments);

    public int Disable(ConsoleAppContext context, [Argument] params string[] arguments) => CommandRuntime.Create(context).Disable(arguments);

    public int Reload(ConsoleAppContext context, [Argument] params string[] arguments) => CommandRuntime.Create(context).Reload(arguments);

    public int Reset(ConsoleAppContext context, [Argument] params string[] arguments) => CommandRuntime.Create(context).Reset(arguments);
}
