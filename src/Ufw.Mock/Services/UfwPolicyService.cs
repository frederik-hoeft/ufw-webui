using Ufw.Mock.Cli;
using Ufw.Mock.State;

namespace Ufw.Mock.Services;

internal sealed class UfwPolicyService(UfwGlobalOptions options, UfwStateStore store, UfwCommandExecutionService execution)
{
    public int SetDefault(IReadOnlyList<string> arguments) => execution.Execute(() =>
    {
        if (arguments.Count is < 1 or > 2)
        {
            throw new UfwCliException("Usage: ufw default allow|deny|reject [incoming|outgoing|routed]");
        }

        string policy = arguments[0].ToUpperInvariant() switch
        {
            "ALLOW" => "allow",
            "DENY" => "deny",
            "REJECT" => "reject",
            _ => throw new UfwCliException($"Invalid default policy '{arguments[0]}'."),
        };
        string direction = arguments.Count == 2
            ? arguments[1].ToUpperInvariant() switch
            {
                "INCOMING" or "INPUT" => "incoming",
                "OUTGOING" or "OUTPUT" => "outgoing",
                "ROUTED" or "FORWARD" => "routed",
                _ => throw new UfwCliException($"Invalid default direction '{arguments[1]}'."),
            }
            : "incoming";

        store.Update(options.DryRun, state =>
        {
            switch (direction)
            {
                case "incoming":
                    state.DefaultIncomingPolicy = policy;
                    break;
                case "outgoing":
                    state.DefaultOutgoingPolicy = policy;
                    break;
                case "routed":
                    state.DefaultRoutedPolicy = policy;
                    break;
            }
            return 0;
        });

        Console.WriteLine($"Default {direction} policy changed to '{policy}'");
        Console.WriteLine("(be sure to update your rules accordingly)");
        return 0;
    });

    public int SetLogging(IReadOnlyList<string> arguments) => execution.Execute(() =>
    {
        if (arguments.Count != 1)
        {
            throw new UfwCliException("Usage: ufw logging on|off|low|medium|high|full");
        }

        string level = arguments[0].ToUpperInvariant() switch
        {
            "ON" => "on",
            "OFF" => "off",
            "LOW" => "low",
            "MEDIUM" => "medium",
            "HIGH" => "high",
            "FULL" => "full",
            _ => throw new UfwCliException($"Invalid log level '{arguments[0]}'."),
        };

        string effectiveLevel = store.Update(options.DryRun, state =>
        {
            if (level == "on")
            {
                if (state.LoggingLevel == "off")
                {
                    state.LoggingLevel = "low";
                }
            }
            else
            {
                state.LoggingLevel = level;
            }
            return state.LoggingLevel;
        });

        Console.WriteLine(effectiveLevel == "off" ? "Logging disabled" : "Logging enabled");
        return 0;
    });
}
