using ConsoleAppFramework;
using Ufw.Mock.Services;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Commands;

internal sealed class RuleCommands(UfwRuleService rules)
{
    public int Allow([Argument] params string[] arguments) =>
        rules.Add(FirewallAction.Allow, arguments, routed: false);

    public int Deny([Argument] params string[] arguments) =>
        rules.Add(FirewallAction.Deny, arguments, routed: false);

    public int Reject([Argument] params string[] arguments) =>
        rules.Add(FirewallAction.Reject, arguments, routed: false);

    public int Limit([Argument] params string[] arguments) =>
        rules.Add(FirewallAction.Limit, arguments, routed: false);

    public int Delete([Argument] params string[] arguments) =>
        rules.Delete(arguments, routed: false);

    public int Insert([Argument] params string[] arguments) =>
        rules.Insert(arguments, routed: false);

    public int Prepend([Argument] params string[] arguments) =>
        rules.Prepend(arguments, routed: false);
}
