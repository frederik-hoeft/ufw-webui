using System.Security.Authentication;

namespace Ufw.Systemd.Configuration.Model;

internal sealed class PipeOptions : IRequireValidation
{
    public string PipeName { get; init; } = "/run/ufw-systemd.pipe";

    public SslProtocols SslProtocols { get; set; }

    public RemoteCertificateValidationOptions? RemoteCertificateValidationOptions { get; set; }

    public required string ServerCertificatePath { get; set; }

    public required string ServerCertificateKeyPath { get; set; }

    public bool AssertIsValid() => this is
    {
        PipeName.Length: > 0,
        ServerCertificateKeyPath.Length: > 0,
        ServerCertificatePath.Length: > 0,
    } && Enum.IsDefined(SslProtocols) 
        && File.Exists(ServerCertificateKeyPath) 
        && File.Exists(ServerCertificatePath) 
        && RemoteCertificateValidationOptions?.AssertIsValid() is not false
        ? true : throw new InvalidOperationException("invalid configuration");
}