using ConsoleAppFramework;

namespace Ufw.Mock.Commands;

internal sealed class ApplicationCommands
{
    public int List(ConsoleAppContext context, [Argument] params string[] arguments) => CommandRuntime.Create(context).AppList(arguments);

    public int Info(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).AppInfo(arguments);

    public int Default(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).AppDefault(arguments);

    public int Update(ConsoleAppContext context, bool addNew = false, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).AppUpdate(addNew, arguments);
}
