using ConsoleAppFramework;

namespace Ufw.Mock.Commands;

internal sealed class StatusCommands
{
    public int Status(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).Status(arguments);

    public int Show(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).Show(arguments);
}
