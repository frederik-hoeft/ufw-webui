using System.Security.Authentication;

namespace Ufw.Ipc.Client.Configuration;

public sealed partial class UfwClientBuilder : IDisposable
{
    private bool _disposedValue;
    private string? _endpointString;
    private SslProtocols _sslProtocols;
    private TimeSpan _ioTimeout = TimeSpan.FromSeconds(15);
    private TimeSpan _requestTimeout = TimeSpan.FromSeconds(15);

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

    public UfwClientBuilder UseIoTimeout(TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        ValidateTimeout(timeout, nameof(timeout));
        _ioTimeout = timeout;
        return this;
    }

    public UfwClientBuilder UseRequestTimeout(TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        ValidateTimeout(timeout, nameof(timeout));
        _requestTimeout = timeout;
        return this;
    }

    private static partial PipeEndpoint ParseEndpoint(string endpoint);

    internal UfwClientOptions Build()
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        _ = _endpointString ?? throw new InvalidOperationException("Required option 'PipeName' was not provided.");

        Dispose();
        PipeEndpoint endpoint = ParseEndpoint(_endpointString);

        return new UfwClientOptions(endpoint.ServerName, endpoint.PipeName, _sslProtocols, _ioTimeout, _requestTimeout);
    }

    private static void ValidateTimeout(TimeSpan timeout, string parameterName)
    {
        if (timeout != Timeout.InfiniteTimeSpan && timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, timeout, "Timeout must be positive or Timeout.InfiniteTimeSpan.");
        }
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
