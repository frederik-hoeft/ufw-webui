using System.Text.RegularExpressions;

namespace Ufw.Ipc.Client.Configuration;

public sealed partial class UfwClientBuilder : IDisposable
{
    [GeneratedRegex("^(?<pipe_name>(/[\\w- \\.]+)+)$")]
    private static partial Regex PipeNameRegex { get; }

    private static partial PipeEndpoint ParseEndpoint(string endpoint)
    {
        Match pipeNameMatch = PipeNameRegex.Match(endpoint);
        if (pipeNameMatch is not { Success: true })
        {
            throw new InvalidOperationException($"Invalid endpoint format: '{endpoint}'");
        }
        string serverName = ".";
        string pipeName = pipeNameMatch.Groups["pipe_name"].Value;
        return new PipeEndpoint(serverName, pipeName);
    }
}
