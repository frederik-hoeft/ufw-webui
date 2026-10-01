using System.Security.Authentication;

namespace Ufw.Systemd.Configuration.Model;

internal sealed class PipeOptions : IRequireValidation
{
    public required string PipeName { get; init; }

    public required bool TlsEnabled { get; init; }

    public required SslProtocols SslProtocols { get; init; }

    public required RemoteCertificateValidationOptions? RemoteCertificateValidation { get; init; }

    public required string? ServerCertificatePath { get; init; }

    public required string? ServerCertificateKeyPath { get; init; }

    public void ThrowIfInvalid()
    {
        if (string.IsNullOrWhiteSpace(PipeName))
        {
            throw new InvalidOperationException("A pipe endpoint is required.");
        }

        if (!TlsEnabled)
        {
            if (RemoteCertificateValidation is not null)
            {
                throw new InvalidOperationException("Client-certificate validation requires TLS to be enabled.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(ServerCertificatePath)
            || string.IsNullOrWhiteSpace(ServerCertificateKeyPath))
        {
            throw new InvalidOperationException("TLS requires server certificate and private-key paths.");
        }

        RemoteCertificateValidation?.ThrowIfInvalid();
    }
}
