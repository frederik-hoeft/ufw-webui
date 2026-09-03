using System.Text.RegularExpressions;

namespace Ufw.Ipc.Client.Configuration;

public sealed partial class UfwClientBuilder : IDisposable
{
    [GeneratedRegex("^\\\\\\\\(?<server_name>[^\\\\/]+)\\\\pipe\\\\(?<pipe_name>[^\\\\/]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PipePathRegex { get; }

    [GeneratedRegex("^//(?<server_name>[^/\\\\]+)/pipe/(?<pipe_name>[^/\\\\]+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AltPipePathRegex { get; }

    [GeneratedRegex("^(?<pipe_name>[^/\\\\]+)$")]
    private static partial Regex PipeNameRegex { get; }

    private static partial PipeEndpoint ParseEndpoint(string endpoint)
    {
        Match pipePathMatch = PipePathRegex.Match(endpoint);
        if (pipePathMatch is { Success: true })
        {
            return new PipeEndpoint(pipePathMatch.Groups["server_name"].Value, pipePathMatch.Groups["pipe_name"].Value);
        }

        Match altPipePathMatch = AltPipePathRegex.Match(endpoint);
        if (altPipePathMatch is { Success: true })
        {
            return new PipeEndpoint(altPipePathMatch.Groups["server_name"].Value, altPipePathMatch.Groups["pipe_name"].Value);
        }

        Match pipeNameMatch = PipeNameRegex.Match(endpoint);
        if (pipeNameMatch is not { Success: true })
        {
            throw new InvalidOperationException($"Invalid endpoint format: '{endpoint}'");
        }

        return new PipeEndpoint(".", pipeNameMatch.Groups["pipe_name"].Value);
    }
}
