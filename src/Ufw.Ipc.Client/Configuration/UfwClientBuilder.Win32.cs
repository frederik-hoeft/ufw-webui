using System.Text.RegularExpressions;

namespace Ufw.Ipc.Client.Configuration;

public sealed partial class UfwClientBuilder : IDisposable
{
    [GeneratedRegex("^//(?<server_name>[^/]+)/pipe/(?<pipe_name>[A-Za-z0-9\\-_]+)$")]
    private static partial Regex PipeUriRegex { get; }

    [GeneratedRegex("^(?<pipe_name>[A-Za-z0-9\\-_]+)$")]
    private static partial Regex PipeNameRegex { get; }

    private static partial PipeEndpoint ParseEndpoint(string endpoint)
    {
        Match pipeUriMatch = PipeUriRegex.Match(endpoint);
        string serverName;
        string pipeName;
        if (pipeUriMatch is { Success: true })
        {
            serverName = pipeUriMatch.Groups["server_name"].Value;
            pipeName = pipeUriMatch.Groups["pipe_name"].Value;
        }
        else
        {
            Match pipeNameMatch = PipeNameRegex.Match(endpoint);
            if (pipeNameMatch is not { Success: true })
            {
                throw new InvalidOperationException($"Invalid endpoint format: '{endpoint}'");
            }

            serverName = ".";
            pipeName = pipeNameMatch.Groups["pipe_name"].Value;
        }
        return new PipeEndpoint(serverName, pipeName);
    }
}
