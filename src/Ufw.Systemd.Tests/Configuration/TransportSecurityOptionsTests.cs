using System.Security.Authentication;
using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Tests.Configuration;

[TestClass]
public sealed class TransportSecurityOptionsTests
{
    [TestMethod]
    public void ThrowIfInvalid_PlaintextDoesNotRequireCertificateFiles()
    {
        TransportSecurityOptions options = CreateOptions();

        options.ThrowIfInvalid();
    }

    [TestMethod]
    public void ThrowIfInvalid_RejectsClientValidationWhenTlsIsDisabled()
    {
        TransportSecurityOptions options = CreateOptions(remoteCertificateValidation: new RemoteCertificateValidationOptions
        {
            RequiredIssuer = "CN=test-ca",
            RequiredSubject = "CN=test-client",
        });

        Assert.ThrowsExactly<InvalidOperationException>(options.ThrowIfInvalid);
    }

    [TestMethod]
    public void ThrowIfInvalid_TlsRequiresCertificatePaths()
    {
        TransportSecurityOptions options = CreateOptions(tlsEnabled: true);

        Assert.ThrowsExactly<InvalidOperationException>(options.ThrowIfInvalid);
    }

    [TestMethod]
    public void ThrowIfInvalid_DoesNotConsultCertificateFilesystem()
    {
        TransportSecurityOptions options = CreateOptions(
            tlsEnabled: true,
            serverCertificatePath: "/definitely/not/a/certificate.pem",
            serverCertificateKeyPath: "/definitely/not/a/key.pem");

        options.ThrowIfInvalid();
    }

    private static TransportSecurityOptions CreateOptions(
        bool tlsEnabled = false,
        RemoteCertificateValidationOptions? remoteCertificateValidation = null,
        string? serverCertificatePath = null,
        string? serverCertificateKeyPath = null) =>
        new()
        {
            TlsEnabled = tlsEnabled,
            SslProtocols = SslProtocols.None,
            RemoteCertificateValidation = remoteCertificateValidation,
            ServerCertificatePath = serverCertificatePath,
            ServerCertificateKeyPath = serverCertificateKeyPath,
        };
}
