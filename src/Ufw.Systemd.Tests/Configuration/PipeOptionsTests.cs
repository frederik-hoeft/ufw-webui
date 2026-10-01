using System.Security.Authentication;
using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Tests.Configuration;

[TestClass]
public sealed class PipeOptionsTests
{
    [TestMethod]
    public void ThrowIfInvalid_PlaintextDoesNotRequireCertificateFiles()
    {
        PipeOptions options = CreateOptions();

        options.ThrowIfInvalid();
    }

    [TestMethod]
    public void ThrowIfInvalid_RejectsClientValidationWhenTlsIsDisabled()
    {
        PipeOptions options = CreateOptions(remoteCertificateValidation: new RemoteCertificateValidationOptions
        {
            RequiredIssuer = "CN=test-ca",
            RequiredSubject = "CN=test-client",
        });

        Assert.ThrowsExactly<InvalidOperationException>(options.ThrowIfInvalid);
    }

    [TestMethod]
    public void ThrowIfInvalid_TlsRequiresCertificatePaths()
    {
        PipeOptions options = CreateOptions(tlsEnabled: true);

        Assert.ThrowsExactly<InvalidOperationException>(options.ThrowIfInvalid);
    }

    [TestMethod]
    public void ThrowIfInvalid_DoesNotConsultCertificateFilesystem()
    {
        PipeOptions options = CreateOptions(
            tlsEnabled: true,
            serverCertificatePath: "/definitely/not/a/certificate.pem",
            serverCertificateKeyPath: "/definitely/not/a/key.pem");

        options.ThrowIfInvalid();
    }

    [TestMethod]
    public void ThrowIfInvalid_DoesNotApplyHostSpecificPipePathRules()
    {
        PipeOptions options = CreateOptions(pipeName: "relative-pipe-name");

        options.ThrowIfInvalid();
    }

    private static PipeOptions CreateOptions(
        string pipeName = "/tmp/ufw-tests.pipe",
        bool tlsEnabled = false,
        RemoteCertificateValidationOptions? remoteCertificateValidation = null,
        string? serverCertificatePath = null,
        string? serverCertificateKeyPath = null) =>
        new()
        {
            PipeName = pipeName,
            TlsEnabled = tlsEnabled,
            SslProtocols = SslProtocols.None,
            RemoteCertificateValidation = remoteCertificateValidation,
            ServerCertificatePath = serverCertificatePath,
            ServerCertificateKeyPath = serverCertificateKeyPath,
        };
}
