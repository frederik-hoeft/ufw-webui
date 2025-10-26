using System.Security.Authentication;
using System.Text.RegularExpressions;

namespace Ufw.Ipc.Client.Configuration;

public sealed partial class UfwClientBuilder : IDisposable
{
    private bool _disposedValue;
    private string? _endpoint;
    private SslProtocols _sslProtocols;

    internal UfwClientBuilder() => Pass();

    [GeneratedRegex("^//(?<server_name>[^/]+)/pipe/(?<pipe_name>[A-Za-z0-9\\-_]+)$")]
    private static partial Regex PipeUriRegex();

    [GeneratedRegex("^(?<pipe_name>[A-Za-z0-9\\-_]+)$")]
    private static partial Regex PipeNameRegex();

    public UfwClientBuilder ConnectTo(string endpoint)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint, nameof(endpoint));
        _endpoint = endpoint;
        return this;
    }

    public UfwClientBuilder UseSsl(SslProtocols sslProtocols)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        _sslProtocols = sslProtocols;
        return this;
    }

    internal UfwClientOptions Build()
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        _ = _endpoint ?? throw new InvalidOperationException("Required option 'PipeName' was not provided.");

        Dispose();
        Match pipeUriMatch = PipeUriRegex().Match(_endpoint);
        string serverName;
        string pipeName;
        if (pipeUriMatch is { Success: true })
        {
            serverName = pipeUriMatch.Groups["server_name"].Value;
            pipeName = pipeUriMatch.Groups["pipe_name"].Value;
        }
        else
        {
            Match pipeNameMatch = PipeNameRegex().Match(_endpoint);
            if (pipeNameMatch is not { Success: true })
            {
                throw new InvalidOperationException($"Invalid endpoint format: '{_endpoint}'");
            }

            serverName = ".";
            pipeName = pipeNameMatch.Groups["pipe_name"].Value;
        }
        return new UfwClientOptions(serverName, pipeName, _sslProtocols);
    }

    public void Dispose()
    {
        if (_disposedValue)
        {
            return;
        }

        _disposedValue = true;
    }
}
