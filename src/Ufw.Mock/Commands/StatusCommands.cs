using ConsoleAppFramework;
using Ufw.Mock.Services;

namespace Ufw.Mock.Commands;

internal sealed class StatusCommands(UfwStatusService status)
{
    public int Status([Argument] params string[] arguments) =>
        status.Status(arguments);

    public int Show([Argument] params string[] arguments) =>
        status.Show(arguments);
}
