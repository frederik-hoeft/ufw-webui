using Ufw.Mock.Cli;
using Ufw.Mock.Formatting;
using Ufw.Mock.State;

namespace Ufw.Mock.Services;

internal sealed class UfwStatusService(UfwStateStore store, UfwCommandExecutionService execution)
{
    private static readonly HashSet<string> s_reports = new(StringComparer.OrdinalIgnoreCase)
    {
        "raw",
        "builtins",
        "before-rules",
        "user-rules",
        "after-rules",
        "logging-rules",
        "listening",
        "added",
    };

    public int Status(IReadOnlyList<string> arguments) => execution.Execute(() =>
    {
        bool verbose = false;
        bool numbered = false;
        if (arguments.Count > 1)
        {
            throw new UfwCliException("Usage: ufw status [verbose|numbered]");
        }
        if (arguments.Count == 1)
        {
            if (arguments[0].Equals("verbose", StringComparison.OrdinalIgnoreCase))
            {
                verbose = true;
            }
            else if (arguments[0].Equals("numbered", StringComparison.OrdinalIgnoreCase))
            {
                numbered = true;
            }
            else
            {
                throw new UfwCliException($"Unknown status mode '{arguments[0]}'.");
            }
        }

        return store.Read(state =>
        {
            Console.WriteLine(UfwOutputFormatter.FormatStatus(state, numbered, verbose));
            return 0;
        });
    });

    public int Show(IReadOnlyList<string> arguments) => execution.Execute(() =>
    {
        if (arguments.Count != 1 || !s_reports.Contains(arguments[0]))
        {
            throw new UfwCliException("Usage: ufw show raw|builtins|before-rules|user-rules|after-rules|logging-rules|listening|added");
        }

        string report = arguments[0].ToUpperInvariant();
        return store.Read(state =>
        {
            string output = report switch
            {
                "ADDED" => UfwOutputFormatter.FormatAdded(state),
                "USER-RULES" => UfwOutputFormatter.FormatUserRules(state),
                "LISTENING" => "Netid  State  Local Address:Port  Peer Address:Port  Process\n# Ufw.Mock does not inspect host sockets.",
                _ => $"# Ufw.Mock synthetic {report} report\n# Firewall status: {(state.Enabled ? "active" : "inactive")}\n# Host netfilter tables are not inspected.",
            };
            Console.WriteLine(output);
            return 0;
        });
    });
}
