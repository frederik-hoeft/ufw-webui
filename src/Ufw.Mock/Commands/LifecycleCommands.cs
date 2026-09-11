using ConsoleAppFramework;
using Ufw.Mock.Services;

namespace Ufw.Mock.Commands;

internal sealed class LifecycleCommands(UfwLifecycleService lifecycle)
{
    public int Enable([Argument] params string[] arguments) => lifecycle.Enable(arguments);

    public int Disable([Argument] params string[] arguments) => lifecycle.Disable(arguments);

    public int Reload([Argument] params string[] arguments) => lifecycle.Reload(arguments);

    public int Reset([Argument] params string[] arguments) => lifecycle.Reset(arguments);
}
