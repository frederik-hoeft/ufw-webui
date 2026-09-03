using Ufw.Ipc.Client.Configuration;

namespace Ufw.Ipc.Tests.Configuration;

[TestClass]
public sealed class UfwClientBuilderTests
{
    private static string TestEndpoint => OperatingSystem.IsWindows()
        ? @"\\.\pipe\ufw-client-builder-tests.pipe"
        : "/tmp/ufw-client-builder-tests.pipe";

    [TestMethod]
    public void TestBuild_ParsesPlatformEndpoint()
    {
        using UfwClientBuilder builder = new();
        UfwClientOptions options = builder.ConnectTo(TestEndpoint).Build();

        Assert.AreEqual(".", options.ServerName);
        Assert.AreEqual(
            OperatingSystem.IsWindows() ? "ufw-client-builder-tests.pipe" : TestEndpoint,
            options.PipeName);
    }

    [TestMethod]
    public void TestBuild_UsesConfiguredTimeouts()
    {
        using UfwClientBuilder builder = new();
        UfwClientOptions options = builder
            .ConnectTo(TestEndpoint)
            .UseIoTimeout(TimeSpan.FromSeconds(3))
            .UseRequestTimeout(TimeSpan.FromSeconds(9))
            .Build();

        Assert.AreEqual(TimeSpan.FromSeconds(3), options.IoTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(9), options.RequestTimeout);
    }

    [TestMethod]
    public void TestTimeoutConfiguration_AcceptsExplicitInfiniteTimeout()
    {
        using UfwClientBuilder builder = new();
        UfwClientOptions options = builder
            .ConnectTo(TestEndpoint)
            .UseIoTimeout(Timeout.InfiniteTimeSpan)
            .UseRequestTimeout(Timeout.InfiniteTimeSpan)
            .Build();

        Assert.AreEqual(Timeout.InfiniteTimeSpan, options.IoTimeout);
        Assert.AreEqual(Timeout.InfiniteTimeSpan, options.RequestTimeout);
    }

    [TestMethod]
    public void TestTimeoutConfiguration_RejectsNonPositiveFiniteTimeout()
    {
        using UfwClientBuilder builder = new();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => builder.UseIoTimeout(TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => builder.UseRequestTimeout(TimeSpan.FromMilliseconds(-2)));
    }
    [TestMethod]
    public void TestBuild_DefaultsToPlaintextTransport()
    {
        using UfwClientBuilder builder = new();
        UfwClientOptions options = builder.ConnectTo(TestEndpoint).Build();

        Assert.IsFalse(options.TlsEnabled);
        Assert.IsNull(options.TlsServerName);
        Assert.AreEqual(System.Security.Authentication.SslProtocols.None, options.SslProtocols);
    }

    [TestMethod]
    public void TestBuild_UseSslNoneEnablesAutomaticTlsSelection()
    {
        using UfwClientBuilder builder = new();
        UfwClientOptions options = builder
            .ConnectTo(TestEndpoint)
            .UseSsl("daemon.test", System.Security.Authentication.SslProtocols.None)
            .Build();

        Assert.IsTrue(options.TlsEnabled);
        Assert.AreEqual("daemon.test", options.TlsServerName);
        Assert.AreEqual(System.Security.Authentication.SslProtocols.None, options.SslProtocols);
    }

    [TestMethod]
    public void TestBuild_LocalTlsRequiresExplicitServerCertificateName()
    {
        using UfwClientBuilder builder = new();
        _ = builder.ConnectTo(TestEndpoint).UseSsl();

        Assert.ThrowsExactly<InvalidOperationException>(() => builder.Build());
    }

    [TestMethod]
    public void TestBuild_ClientCertificateRequiresTls()
    {
        string certificatePath = Path.GetTempFileName();
        string keyPath = Path.GetTempFileName();
        try
        {
            using UfwClientBuilder builder = new();
            _ = builder
                .ConnectTo(TestEndpoint)
                .UseClientCertificate(certificatePath, keyPath);

            Assert.ThrowsExactly<InvalidOperationException>(() => builder.Build());
        }
        finally
        {
            File.Delete(certificatePath);
            File.Delete(keyPath);
        }
    }

    [TestMethod]
    public void TestBuild_ClientCertificateFilesMustExist()
    {
        using UfwClientBuilder builder = new();
        _ = builder
            .ConnectTo(TestEndpoint)
            .UseSsl("daemon.test")
            .UseClientCertificate("/nonexistent/client-cert.pem", "/nonexistent/client-key.pem");

        Assert.ThrowsExactly<InvalidOperationException>(() => builder.Build());
    }
}
