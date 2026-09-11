using Ufw.Mock.Cli;
using Ufw.Mock.Rules;
using Ufw.Mock.State;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Services;

internal sealed class UfwApplicationService(
    UfwGlobalOptions options,
    UfwStateStore store,
    UfwRuleMutationCoordinator mutations,
    UfwRuleMutationReporter reporter,
    UfwCommandExecutionService execution)
{
    public int List(IReadOnlyList<string> arguments) => execution.Execute(() =>
    {
        RequireNoArguments(arguments, "ufw app list");
        return store.Read(state =>
        {
            Console.WriteLine("Available applications:");
            foreach (UfwApplicationProfile profile in state.ApplicationProfiles.OrderBy(static profile => profile.Name, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine("  " + profile.Name);
            }
            return 0;
        });
    });

    public int Info(IReadOnlyList<string> arguments) => execution.Execute(() =>
    {
        if (arguments.Count != 1)
        {
            throw new UfwCliException("Usage: ufw app info PROFILE|all");
        }

        return store.Read(state =>
        {
            IReadOnlyList<UfwApplicationProfile> profiles;
            if (arguments[0].Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                profiles = [.. state.ApplicationProfiles.OrderBy(static profile => profile.Name, StringComparer.OrdinalIgnoreCase)];
            }
            else
            {
                UfwApplicationProfile profile = state.ApplicationProfiles.FirstOrDefault(profile => profile.Name.Equals(arguments[0], StringComparison.OrdinalIgnoreCase))
                    ?? throw new UfwCliException($"Could not find profile '{arguments[0]}'");
                profiles = [profile];
            }

            for (int index = 0; index < profiles.Count; index++)
            {
                if (index != 0)
                {
                    Console.WriteLine();
                }
                UfwApplicationProfile profile = profiles[index];
                Console.WriteLine("Profile: " + profile.Name);
                Console.WriteLine("Title: " + profile.Title);
                Console.WriteLine("Description: " + profile.Description);
                Console.WriteLine();
                string[] ports = profile.Ports.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                Console.WriteLine(ports.Length > 1 || ports[0].Contains(',', StringComparison.Ordinal) ? "Ports:" : "Port:");
                foreach (string port in ports)
                {
                    Console.WriteLine("  " + port);
                }
            }
            return 0;
        });
    });

    public int SetDefault(IReadOnlyList<string> arguments) => execution.Execute(() =>
    {
        if (arguments.Count != 1)
        {
            throw new UfwCliException("Usage: ufw app default allow|deny|reject|skip");
        }

        string policy = arguments[0].ToUpperInvariant() switch
        {
            "ALLOW" => "allow",
            "DENY" => "deny",
            "REJECT" => "reject",
            "SKIP" => "skip",
            _ => throw new UfwCliException($"Invalid application policy '{arguments[0]}'."),
        };
        store.Update(options.DryRun, state =>
        {
            state.DefaultApplicationPolicy = policy;
            return 0;
        });
        Console.WriteLine("Default application policy changed to '" + policy + "'");
        return 0;
    });

    public int Update(bool addNew, IReadOnlyList<string> arguments) => execution.Execute(() =>
    {
        if (arguments.Count != 1)
        {
            throw new UfwCliException("Usage: ufw app update [--add-new] PROFILE|all");
        }
        if (addNew && arguments[0].Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            throw new UfwCliException("Cannot specify 'all' with '--add-new'");
        }

        List<string> names = store.Read(state =>
        {
            if (arguments[0].Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return state.ApplicationProfiles.Select(static profile => profile.Name).ToList();
            }
            UfwApplicationProfile profile = state.ApplicationProfiles.FirstOrDefault(profile => profile.Name.Equals(arguments[0], StringComparison.OrdinalIgnoreCase))
                ?? throw new UfwCliException($"Could not find profile '{arguments[0]}'");
            return [profile.Name];
        });

        foreach (string name in names)
        {
            UpdateProfile(name, addNew);
        }
        return 0;
    });

    private void UpdateProfile(string name, bool addNew)
    {
        string policy = store.Read(static state => state.DefaultApplicationPolicy);
        Console.WriteLine("Rules updated for profile '" + name + "'");
        if (!addNew || policy == "skip")
        {
            return;
        }

        bool exists = store.Read(state => state.Rules.Any(rule =>
            string.Equals(rule.SourceApplicationName, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(rule.DestinationApplicationName, name, StringComparison.OrdinalIgnoreCase)));
        if (exists)
        {
            return;
        }

        FirewallAction action = policy switch
        {
            "allow" => FirewallAction.Allow,
            "deny" => FirewallAction.Deny,
            "reject" => FirewallAction.Reject,
            _ => throw new UfwCliException($"Unsupported application policy '{policy}'."),
        };
        reporter.WriteResults(mutations.Add(action, [name], routed: false));
    }

    private static void RequireNoArguments(IReadOnlyList<string> arguments, string usage)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count != 0)
        {
            throw new UfwCliException($"Usage: {usage}");
        }
    }
}
