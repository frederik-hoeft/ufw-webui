using Ufw.Mock.Cli;
using Ufw.Mock.State;
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
        if (arguments.Length == 1 && arguments[0] is "--version" or "-V")
        {
            Console.Out.WriteLine(executable + " v1.8.10 (nf_tables, Ufw.Mock)");
            return 0;
        }
        if (arguments.Length == 1 && arguments[0] is "--help" or "-h")
        {
            Console.Out.WriteLine("Usage: " + executable + " [-t filter] [-c]");
            return 0;
        }
        bool tableSeen = false;
        for (int index = 0; index < arguments.Length; index++)
        {
            if (arguments[index] is "-c" or "--counters")
            {
                continue;
            }
            if (!tableSeen && arguments[index] is "-t" or "--table" && index + 1 < arguments.Length && arguments[index + 1] == "filter")
            {
                tableSeen = true;
                index++;
                continue;
            }
            Console.Error.WriteLine(executable + ": unsupported arguments");
            return 2;
        }
        try
        {
            UfwStateStore store = new();
            string output = store.Read(state =>
            {
                MockFilterTable table = new(state, family);
                table.Add(state, family);
                return table.Format();
            });
            Console.Out.Write(output);
            return 0;
        }
        catch (Exception exception) when (exception is UfwCliException or IOException or UnauthorizedAccessException or FormatException)
        {
            Console.Error.WriteLine(executable + ": " + exception.Message);
            return 1;
        }
    }
}
