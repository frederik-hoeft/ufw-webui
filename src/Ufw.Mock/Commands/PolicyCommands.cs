using ConsoleAppFramework;
using Ufw.Mock.Services;

namespace Ufw.Mock.Commands;

internal sealed class PolicyCommands(UfwPolicyService policies)
{
    public int Default([Argument] params string[] arguments) =>
        policies.SetDefault(arguments);

    public int Logging([Argument] params string[] arguments) =>
        policies.SetLogging(arguments);
}
