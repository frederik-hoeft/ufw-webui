using System.Text.RegularExpressions;

namespace Ufw.Ipc.Client.Configuration;

public sealed partial class UfwClientBuilder : IDisposable
{
    [GeneratedRegex(
        "^\\\\\\\\(?<server_name>[^\\\\/]+)\\\\pipe\\\\(?<pipe_name>[^\\\\/]+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PipePathRegex { get; }

    [GeneratedRegex(
        "^//(?<server_name>[^/\\\\]+)/pipe/(?<pipe_name>[^/\\\\]+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LegacyPipePathRegex { get; }

    [GeneratedRegex("^(?<pipe_name>[^/\\\\]+)$")]
    private static partial Regex PipeNameRegex { get; }

    private static partial PipeEndpoint ParseEndpoint(string endpoint)
    {
        Match pipePathMatch = PipePathRegex.Match(endpoint);
        if (pipePathMatch is { Success: true })
        {
            return new PipeEndpoint(
                pipePathMatch.Groups["server_name"].Value,
                pipePathMatch.Groups["pipe_name"].Value);
        }

        Match legacyPipePathMatch = LegacyPipePathRegex.Match(endpoint);
        if (legacyPipePathMatch is { Success: true })
        {
            return new PipeEndpoint(
                legacyPipePathMatch.Groups["server_name"].Value,
                legacyPipePathMatch.Groups["pipe_name"].Value);
        }

        Match pipeNameMatch = PipeNameRegex.Match(endpoint);
        if (pipeNameMatch is not { Success: true })
        {
            throw new InvalidOperationException($"Invalid endpoint format: '{endpoint}'");
        }

        return new PipeEndpoint(".", pipeNameMatch.Groups["pipe_name"].Value);
    }
}
