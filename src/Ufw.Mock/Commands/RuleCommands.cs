using ConsoleAppFramework;
using Ufw.Mock.Cli;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Commands;

internal sealed class RuleCommands(UfwCommandExecutor executor)
{
    public int Allow([Argument] params string[] arguments) =>
        executor.Add(FirewallAction.Allow, arguments, routed: false);

    public int Deny([Argument] params string[] arguments) =>
        executor.Add(FirewallAction.Deny, arguments, routed: false);

    public int Reject([Argument] params string[] arguments) =>
        executor.Add(FirewallAction.Reject, arguments, routed: false);

    public int Limit([Argument] params string[] arguments) =>
        executor.Add(FirewallAction.Limit, arguments, routed: false);

    public int Delete([Argument] params string[] arguments) =>
        executor.Delete(arguments, routed: false);

    public int Insert([Argument] params string[] arguments) =>
        executor.Insert(arguments, routed: false);

    public int Prepend([Argument] params string[] arguments) =>
        executor.Prepend(arguments, routed: false);
}
