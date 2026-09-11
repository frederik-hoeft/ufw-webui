using ConsoleAppFramework;
using Ufw.Mock.Services;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Commands;

internal sealed class RouteCommands(UfwRuleService rules)
{
    public int Allow([Argument] params string[] arguments) =>
        rules.Add(FirewallAction.Allow, arguments, routed: true);

    public int Deny([Argument] params string[] arguments) =>
        rules.Add(FirewallAction.Deny, arguments, routed: true);

    public int Reject([Argument] params string[] arguments) =>
        rules.Add(FirewallAction.Reject, arguments, routed: true);

    public int Limit([Argument] params string[] arguments) =>
        rules.Add(FirewallAction.Limit, arguments, routed: true);

    public int Delete([Argument] params string[] arguments) =>
        rules.Delete(arguments, routed: true);

    public int Insert([Argument] params string[] arguments) =>
        rules.Insert(arguments, routed: true);

    public int Prepend([Argument] params string[] arguments) =>
        rules.Prepend(arguments, routed: true);
}
