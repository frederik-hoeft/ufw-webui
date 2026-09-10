using ConsoleAppFramework;
using Ufw.Mock.Cli;

namespace Ufw.Mock.Commands;

internal sealed class ApplicationCommands(UfwCommandExecutor executor)
{
    public int List([Argument] params string[] arguments) => executor.AppList(arguments);

    public int Info([Argument] params string[] arguments) =>
        executor.AppInfo(arguments);

    public int Default([Argument] params string[] arguments) =>
        executor.AppDefault(arguments);

    public int Update(bool addNew = false, [Argument] params string[] arguments) =>
        executor.AppUpdate(addNew, arguments);
}
