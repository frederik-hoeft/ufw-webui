using ConsoleAppFramework;
using Ufw.Mock.Commands;

namespace Ufw.Mock;

internal sealed class UfwCommandBuilder
{
    private readonly ConsoleApp.ConsoleAppBuilder _builder;

    public UfwCommandBuilder(ConsoleApp.ConsoleAppBuilder builder)
    {
        _builder = builder;
    }

    public UfwCommandBuilder Add<T>()
    {
        if (typeof(T) == typeof(LifecycleCommands))
        {
            _builder.Add<LifecycleCommands>();
        }
        else if (typeof(T) == typeof(PolicyCommands))
        {
            _builder.Add<PolicyCommands>();
        }
        else if (typeof(T) == typeof(StatusCommands))
        {
            _builder.Add<StatusCommands>();
        }
        else if (typeof(T) == typeof(RuleCommands))
        {
            _builder.Add<RuleCommands>();
        }
        else
        {
            throw new NotSupportedException($"Unsupported root command category '{typeof(T).FullName}'.");
        }
        return this;
    }

    public UfwCommandBuilder Add<T>(string commandPath)
    {
        if (typeof(T) == typeof(ExplicitRuleCommands) && commandPath == "rule")
        {
            _builder.Add<ExplicitRuleCommands>("rule");
        }
        else if (typeof(T) == typeof(RouteCommands) && commandPath == "route")
        {
            _builder.Add<RouteCommands>("route");
        }
        else if (typeof(T) == typeof(ApplicationCommands) && commandPath == "app")
        {
            _builder.Add<ApplicationCommands>("app");
        }
        else
        {
            throw new NotSupportedException($"Unsupported command category '{typeof(T).FullName}' at '{commandPath}'.");
        }
        return this;
    }

    public Task RunAsync(string[] args) => _builder.RunAsync(args);
}
