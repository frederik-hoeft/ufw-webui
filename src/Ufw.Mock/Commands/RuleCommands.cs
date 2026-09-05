using ConsoleAppFramework;
using Ufw.Ipc.Shared.Model.Domain.Rules;

namespace Ufw.Mock.Commands;

internal sealed class RuleCommands
{
    public int Allow(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).Add(FirewallAction.Allow, arguments, routed: false);

    public int Deny(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).Add(FirewallAction.Deny, arguments, routed: false);

    public int Reject(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).Add(FirewallAction.Reject, arguments, routed: false);

    public int Limit(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).Add(FirewallAction.Limit, arguments, routed: false);

    public int Delete(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).Delete(arguments, routed: false);

    public int Insert(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).Insert(arguments, routed: false);

    public int Prepend(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).Prepend(arguments, routed: false);
}
