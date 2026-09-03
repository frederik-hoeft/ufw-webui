using Ufw.Web.Configuration;

namespace Ufw.Web.Tests.Configuration;

[TestClass]
public sealed class IpcClientOptionsTests
{
    [TestMethod]
    public void IsValid_PlaintextDefaultsAreValid()
    {
        Assert.IsTrue(new IpcClientOptions().IsValid());
    }

    [TestMethod]
    public void IsValid_TlsRequiresServerCertificateName()
    {
        IpcClientOptions options = new()
        {
            TlsEnabled = true,
        };

        Assert.IsFalse(options.IsValid());
        options.TlsServerName = "daemon.test";
        Assert.IsTrue(options.IsValid());
    }

    [TestMethod]
    public void IsValid_RejectsPartialOrPlaintextClientCertificateConfiguration()
    {
        IpcClientOptions options = new()
        {
            ClientCertificatePath = "/cert.pem",
        };
        Assert.IsFalse(options.IsValid());

        options.ClientCertificateKeyPath = "/key.pem";
        Assert.IsFalse(options.IsValid());

        options.TlsEnabled = true;
        options.TlsServerName = "daemon.test";
        Assert.IsTrue(options.IsValid());
    }
}
