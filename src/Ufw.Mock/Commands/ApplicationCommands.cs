using ConsoleAppFramework;
using Ufw.Mock.Services;

namespace Ufw.Mock.Commands;

internal sealed class ApplicationCommands(UfwApplicationService applications)
{
    public int List([Argument] params string[] arguments) => applications.List(arguments);

    public int Info([Argument] params string[] arguments) =>
        applications.Info(arguments);

    public int Default([Argument] params string[] arguments) =>
        applications.SetDefault(arguments);

    public int Update(bool addNew = false, [Argument] params string[] arguments) =>
        applications.Update(addNew, arguments);
}
