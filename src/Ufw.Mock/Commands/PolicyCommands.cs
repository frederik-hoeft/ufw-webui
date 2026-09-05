using ConsoleAppFramework;

namespace Ufw.Mock.Commands;

internal sealed class PolicyCommands
{
    public int Default(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).SetDefault(arguments);

    public int Logging(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).SetLogging(arguments);
}
