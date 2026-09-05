using ConsoleAppFramework;
using Ufw.Mock.Commands;

namespace Ufw.Mock;

public static class UfwMockApplication
{
    private static readonly HashSet<string> s_rootCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "enable", "disable", "reload", "reset", "default", "logging", "status", "show",
        "allow", "deny", "reject", "limit", "delete", "insert", "prepend", "rule", "route", "app",
    };

    public const string COMPATIBILITY_VERSION = "0.36.2";

    public static async Task<int> RunAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        int previousExitCode = Environment.ExitCode;
        try
        {
            Environment.ExitCode = 0;
            ConsoleApp.Version = $"ufw {COMPATIBILITY_VERSION} (Ufw.Mock)";

            ConsoleApp.ConsoleAppBuilder app = ConsoleApp.Create();
            app.ConfigureGlobalOptions((ref ConsoleApp.GlobalOptionsBuilder builder) =>
            {
                bool dryRun = builder.AddGlobalOption<bool>("--dry-run");
                bool force = builder.AddGlobalOption<bool>("--force");
                return new UfwGlobalOptions(dryRun, force);
            });

            UfwCommandBuilder builder = new(app);
            builder
                .Add<LifecycleCommands>()
                .Add<PolicyCommands>()
                .Add<StatusCommands>()
                .Add<RuleCommands>()
                .Add<ExplicitRuleCommands>("rule")
                .Add<RouteCommands>("route")
                .Add<ApplicationCommands>("app");

            string[] normalizedArgs = NormalizeLeadingGlobalOptions(args);
            if (!HasValidRootCommand(normalizedArgs))
            {
                await Console.Error.WriteLineAsync("ERROR: Invalid syntax");
                return 1;
            }

            try
            {
                await builder.RunAsync(normalizedArgs);
                return Environment.ExitCode;
            }
            catch (Cli.UfwCliException exception)
            {
                await Console.Error.WriteLineAsync("ERROR: " + exception.Message);
                return 1;
            }
        }
        finally
        {
            Environment.ExitCode = previousExitCode;
        }
    }

    private static bool HasValidRootCommand(string[] args)
    {
        if (args.Length == 0)
        {
            return true;
        }

        string first = args[0];
        return first is "-h" or "--help" or "--version" || s_rootCommands.Contains(first);
    }

    private static string[] NormalizeLeadingGlobalOptions(string[] args)
    {
        if (args.Length == 0)
        {
            return args;
        }

        List<string> leadingGlobalOptions = [];
        int index = 0;
        while (index < args.Length
            && (args[index] == "--dry-run" || args[index] == "--force"))
        {
            leadingGlobalOptions.Add(args[index]);
            index++;
        }

        if (leadingGlobalOptions.Count == 0 || index == args.Length)
        {
            return args;
        }

        return [.. args[index..], .. leadingGlobalOptions];
    }
}
