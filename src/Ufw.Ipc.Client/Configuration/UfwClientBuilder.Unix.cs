namespace Ufw.Ipc.Client.Configuration;

public sealed partial class UfwClientBuilder : IDisposable
{
    private static partial PipeEndpoint ParseEndpoint(string endpoint)
    {
        if (!Path.IsPathFullyQualified(endpoint) || Path.EndsInDirectorySeparator(endpoint))
        {
            throw new InvalidOperationException($"Invalid endpoint format: '{endpoint}'");
        }

        return new PipeEndpoint(".", endpoint);
    }
}
