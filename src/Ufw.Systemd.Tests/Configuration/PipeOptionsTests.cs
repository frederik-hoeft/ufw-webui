using System.Security.Authentication;
using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Tests.Configuration;

[TestClass]
public sealed class PipeOptionsTests
{
    [TestMethod]
    public void AssertIsValid_PlaintextDoesNotRequireCertificateFiles()
    {
        PipeOptions options = new()
        {
            PipeName = "/tmp/ufw-tests.pipe",
            TlsEnabled = false,
        };

        Assert.IsTrue(options.AssertIsValid());
    }

    [TestMethod]
    public void AssertIsValid_RejectsClientValidationWhenTlsIsDisabled()
    {
        PipeOptions options = new()
        {
            PipeName = "/tmp/ufw-tests.pipe",
            TlsEnabled = false,
            RemoteCertificateValidation = new RemoteCertificateValidationOptions
            {
                RequiredIssuer = "CN=test-ca",
                RequiredSubject = "CN=test-client",
            },
        };

        Assert.ThrowsExactly<InvalidOperationException>(() => options.AssertIsValid());
    }

    [TestMethod]
    public void AssertIsValid_TlsRequiresCertificateFiles()
    {
        PipeOptions options = new()
        {
            PipeName = "/tmp/ufw-tests.pipe",
            TlsEnabled = true,
            SslProtocols = SslProtocols.None,
        };

        Assert.ThrowsExactly<InvalidOperationException>(() => options.AssertIsValid());
    }

    [TestMethod]
    public void AssertIsValid_AcceptsAutomaticProtocolSelectionWhenTlsIsEnabled()
    {
        string certificatePath = Path.GetTempFileName();
        string keyPath = Path.GetTempFileName();
        try
        {
            PipeOptions options = new()
            {
                PipeName = "/tmp/ufw-tests.pipe",
                TlsEnabled = true,
                SslProtocols = SslProtocols.None,
                ServerCertificatePath = certificatePath,
                ServerCertificateKeyPath = keyPath,
            };

            Assert.IsTrue(options.AssertIsValid());
        }
        finally
        {
            File.Delete(certificatePath);
            File.Delete(keyPath);
        }
    }
}
