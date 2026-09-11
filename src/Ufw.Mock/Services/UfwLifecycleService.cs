using Ufw.Mock.Cli;
using Ufw.Mock.State;

namespace Ufw.Mock.Services;

internal sealed class UfwLifecycleService(UfwGlobalOptions options, UfwStateStore store, UfwConfirmationService confirmation, UfwCommandExecutionService execution)
{
    public int Enable(IReadOnlyList<string> arguments) => execution.Execute(() =>
    {
        RequireNoArguments(arguments, "ufw enable");
        store.Update(options.DryRun, state =>
        {
            state.Enabled = true;
            return 0;
        });
        Console.WriteLine("Firewall is active and enabled on system startup");
        return 0;
    });

    public int Disable(IReadOnlyList<string> arguments) => execution.Execute(() =>
    {
        RequireNoArguments(arguments, "ufw disable");
        store.Update(options.DryRun, state =>
        {
            state.Enabled = false;
            return 0;
        });
        Console.WriteLine("Firewall stopped and disabled on system startup");
        return 0;
    });

    public int Reload(IReadOnlyList<string> arguments) => execution.Execute(() =>
    {
        RequireNoArguments(arguments, "ufw reload");
        return store.Read(state =>
        {
            Console.WriteLine(state.Enabled ? "Firewall reloaded" : "Firewall not enabled (skipping reload)");
            return 0;
        });
    });

    public int Reset(IReadOnlyList<string> arguments) => execution.Execute(() =>
    {
        RequireNoArguments(arguments, "ufw reset");
        if (!options.Force && !confirmation.Confirm("Resetting all rules to installed defaults. Proceed with operation (y|n)? "))
        {
            Console.WriteLine("Aborted");
            return 0;
        }

        store.Update(options.DryRun, state =>
        {
            UfwMockState defaults = UfwMockState.CreateDefault();
            state.SchemaVersion = defaults.SchemaVersion;
            state.Enabled = defaults.Enabled;
            state.LoggingLevel = defaults.LoggingLevel;
            state.DefaultIncomingPolicy = defaults.DefaultIncomingPolicy;
            state.DefaultOutgoingPolicy = defaults.DefaultOutgoingPolicy;
            state.DefaultRoutedPolicy = defaults.DefaultRoutedPolicy;
            state.DefaultApplicationPolicy = defaults.DefaultApplicationPolicy;
            state.IPv6Enabled = defaults.IPv6Enabled;
            state.Rules.Clear();
            return 0;
        });
        Console.WriteLine("Resetting all rules to installed defaults");
        return 0;
    });

    private static void RequireNoArguments(IReadOnlyList<string> arguments, string usage)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count != 0)
        {
            throw new UfwCliException($"Usage: {usage}");
        }
    }
}
