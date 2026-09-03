using System.Security.Authentication;

namespace Ufw.Ipc.Client.Configuration;

public sealed partial class UfwClientBuilder : IDisposable
{
    private bool _disposedValue;
    private string? _endpointString;
    private bool _tlsEnabled;
    private string? _tlsServerName;
    private SslProtocols _sslProtocols = SslProtocols.None;
    private TimeSpan _ioTimeout = TimeSpan.FromSeconds(15);
    private TimeSpan _requestTimeout = TimeSpan.FromSeconds(15);
    private string? _clientCertificatePath;
    private string? _clientCertificateKeyPath;

    internal UfwClientBuilder() => Pass();

    public UfwClientBuilder ConnectTo(string endpoint)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint, nameof(endpoint));
        _endpointString = endpoint;
        return this;
    }

    public UfwClientBuilder UseSsl(SslProtocols sslProtocols = SslProtocols.None)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        _tlsEnabled = true;
        _sslProtocols = sslProtocols;
        return this;
    }

    public UfwClientBuilder UseSsl(string serverName, SslProtocols sslProtocols = SslProtocols.None)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        _tlsEnabled = true;
        _tlsServerName = serverName;
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

    public UfwClientBuilder UseClientCertificate(string certificatePath, string keyPath)
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(certificatePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        _clientCertificatePath = certificatePath;
        _clientCertificateKeyPath = keyPath;
        return this;
    }

    private static partial PipeEndpoint ParseEndpoint(string endpoint);

    internal UfwClientOptions Build()
    {
        ObjectDisposedException.ThrowIf(_disposedValue, this);
        _ = _endpointString ?? throw new InvalidOperationException("Required option 'PipeName' was not provided.");

        ValidateCertificateConfiguration();
        PipeEndpoint endpoint = ParseEndpoint(_endpointString);
        string? tlsServerName = ResolveTlsServerName(endpoint);
        UfwClientOptions options = new(
            endpoint.ServerName,
            endpoint.PipeName,
            _tlsEnabled,
            tlsServerName,
            _sslProtocols,
            _ioTimeout,
            _requestTimeout,
            _clientCertificatePath,
            _clientCertificateKeyPath);
        Dispose();
        return options;
    }

    private string? ResolveTlsServerName(PipeEndpoint endpoint)
    {
        if (!_tlsEnabled)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(_tlsServerName))
        {
            return _tlsServerName;
        }

        if (!string.Equals(endpoint.ServerName, ".", StringComparison.Ordinal))
        {
            return endpoint.ServerName;
        }

        throw new InvalidOperationException("TLS on a local pipe requires an explicit server certificate name.");
    }

    private void ValidateCertificateConfiguration()
    {
        bool clientCertificateConfigured = _clientCertificatePath is not null || _clientCertificateKeyPath is not null;
        if (clientCertificateConfigured && !_tlsEnabled)
        {
            throw new InvalidOperationException("A client certificate can only be configured when TLS is enabled.");
        }

        if (!clientCertificateConfigured)
        {
            return;
        }

        if (!File.Exists(_clientCertificatePath) || !File.Exists(_clientCertificateKeyPath))
        {
            throw new InvalidOperationException("Configured client certificate and private-key files must exist.");
        }
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
