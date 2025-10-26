using System.Security.Authentication;

namespace Ufw.Ipc.Client.Configuration;

public sealed partial class UfwClientBuilder : IDisposable
{
    private bool _disposedValue;
    private string? _endpointString;
    private SslProtocols _sslProtocols;

    internal UfwClientBuilder() => Pass();

    public UfwClientBuilder ConnectTo(string endpoint)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint, nameof(endpoint));
        _endpointString = endpoint;
        return this;
    }

    public UfwClientBuilder UseSsl(SslProtocols sslProtocols)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        _sslProtocols = sslProtocols;
        return this;
    }

    private static partial PipeEndpoint ParseEndpoint(string endpoint);

    internal UfwClientOptions Build()
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        _ = _endpointString ?? throw new InvalidOperationException("Required option 'PipeName' was not provided.");

        Dispose();
        PipeEndpoint endpoint = ParseEndpoint(_endpointString);

        return new UfwClientOptions(endpoint.ServerName, endpoint.PipeName, _sslProtocols);
    }

    public void Dispose()
    {
        if (_disposedValue)
        {
            return;
        }

        _disposedValue = true;
    }

    private readonly record struct PipeEndpoint(string ServerName, string PipeName);
}
