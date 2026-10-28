using ConsoleAppFramework;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Iptables;

/// <summary>Black-box command surface for iptables-save and ip6tables-save.</summary>
public static class IptablesSaveApplication
{
    public static int Run(FirewallAddressFamily family, string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        string executable = family switch
        {
            FirewallAddressFamily.IPv4 => "iptables-save",
            FirewallAddressFamily.IPv6 => "ip6tables-save",
            _ => throw new ArgumentOutOfRangeException(nameof(family)),
        };

        // CAF settings are process-wide; preserve them for repeated in-process mock invocations.
        int previousExitCode = Environment.ExitCode;
        string? previousVersion = ConsoleApp.Version;
        Action<string> previousLogError = ConsoleApp.LogError;
        bool argumentError = false;
        try
        {
            Environment.ExitCode = 0;
            ConsoleApp.Version = executable + " v1.8.10 (nf_tables, Ufw.Mock)";
            ConsoleApp.LogError = message =>
            {
                argumentError = true;
                Console.Error.WriteLine(executable + ": " + message);
            };
            ConsoleApp.ConsoleAppBuilder app = ConsoleApp.Create();
            app.Add("", new IptablesSaveCommand(executable, family).Save);
            app.Run(arguments is ["-V"] ? ["--version"] : arguments);
            return argumentError ? 2 : Environment.ExitCode;
        }
        finally
        {
            ConsoleApp.LogError = previousLogError;
            ConsoleApp.Version = previousVersion;
            Environment.ExitCode = previousExitCode;
        }
    }
}
