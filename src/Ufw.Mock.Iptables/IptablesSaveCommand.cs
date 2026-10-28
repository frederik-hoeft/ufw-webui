using Ufw.Mock.Cli;
using Ufw.Mock.Filtering;
using Ufw.Mock.State;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Iptables;

/// <summary>Produces the active filter table from the shared persisted mock state.</summary>
internal sealed class IptablesSaveCommand(string executable, FirewallAddressFamily family)
{
    /// <summary>Dump the active filter table.</summary>
    /// <param name="table">-t, Table to dump (only filter is supported).</param>
    /// <param name="counters">-c, Include counters.</param>
    public int Save(string? table = null, bool counters = false)
    {
        if (table is not null and not "filter")
        {
            Console.Error.WriteLine(executable + ": unsupported arguments");
            return 2;
        }

        try
        {
            UfwStateStore store = new();
            string output = store.ReadInstalled(state =>
            {
                return MockInstalledFilterState.Observe(state, family).Format();
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
