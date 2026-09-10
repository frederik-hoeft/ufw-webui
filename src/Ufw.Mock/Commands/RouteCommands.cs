using ConsoleAppFramework;
using Ufw.Mock.Cli;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Commands;

internal sealed class RouteCommands(UfwCommandExecutor executor)
{
    public int Allow([Argument] params string[] arguments) =>
        executor.Add(FirewallAction.Allow, arguments, routed: true);

    public int Deny([Argument] params string[] arguments) =>
        executor.Add(FirewallAction.Deny, arguments, routed: true);

    public int Reject([Argument] params string[] arguments) =>
        executor.Add(FirewallAction.Reject, arguments, routed: true);

    public int Limit([Argument] params string[] arguments) =>
        executor.Add(FirewallAction.Limit, arguments, routed: true);

    public int Delete([Argument] params string[] arguments) =>
        executor.Delete(arguments, routed: true);

    public int Insert([Argument] params string[] arguments) =>
        executor.Insert(arguments, routed: true);

    public int Prepend([Argument] params string[] arguments) =>
        executor.Prepend(arguments, routed: true);
}
