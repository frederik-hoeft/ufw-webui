using ConsoleAppFramework;
using Ufw.Ipc.Shared.Model.Domain.Rules;

namespace Ufw.Mock.Commands;

internal sealed class RouteCommands
{
    public int Allow(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).Add(FirewallAction.Allow, arguments, routed: true);

    public int Deny(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).Add(FirewallAction.Deny, arguments, routed: true);

    public int Reject(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).Add(FirewallAction.Reject, arguments, routed: true);

    public int Limit(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).Add(FirewallAction.Limit, arguments, routed: true);

    public int Delete(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).Delete(arguments, routed: true);

    public int Insert(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).Insert(arguments, routed: true);

    public int Prepend(ConsoleAppContext context, [Argument] params string[] arguments) =>
        CommandRuntime.Create(context).Prepend(arguments, routed: true);
}
