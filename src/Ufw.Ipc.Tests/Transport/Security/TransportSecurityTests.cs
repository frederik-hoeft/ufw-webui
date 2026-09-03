using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Ufw.Ipc.Client.Configuration;
using Ufw.Ipc.Client.Transport.Security;
using Ufw.Ipc.Shared.Security.Certificates;
using Ufw.Ipc.Tests.Adapter.Configuration;
using Ufw.Ipc.Tests.Adapter.Transport;
using Ufw.Systemd.Configuration.Model;
using Ufw.Systemd.Transport.Security;
using ClientCertificateValidationHandler = Ufw.Ipc.Client.Transport.Security.CertificateValidation.IRemoteCertificateValidationHandler;
using ClientDefaultCertificateValidationHandler = Ufw.Ipc.Client.Transport.Security.CertificateValidation.DefaultRemoteCertificateValidationHandler;
using ServerCertificateValidationHandler = Ufw.Systemd.Transport.Security.CertificateValidation.IRemoteCertificateValidationHandler;
using ServerMutualTlsCertificateValidationHandler = Ufw.Systemd.Transport.Security.CertificateValidation.MutualTlsRemoteCertificateValidationHandler;

namespace Ufw.Ipc.Tests.Transport.Security;

[TestClass]
public sealed class TransportSecurityTests
{
    [TestMethod]
    public async Task DisabledTls_ReturnsUnderlyingStreamsUnchangedAsync()
    {
        using MemoryStream clientInner = new();
        using MemoryStream serverInner = new();
        CertificateValidationHandler validation = new(accept: true);
        PemCertificateLoader certificateLoader = new();

        UfwClientOptions clientOptions = new(
            ServerName: ".",
            PipeName: "/tmp/ufw-tests.pipe",
            TlsEnabled: false,
            TlsServerName: null,
            SslProtocols: SslProtocols.None,
            IoTimeout: TimeSpan.FromSeconds(1),
            RequestTimeout: TimeSpan.FromSeconds(1));
        using ClientTransportSecurityService client = new(validation, certificateLoader, clientOptions);

        AppSettings settings = TestAppSettingsFactory.Create();
        using ServerTransportSecurityService server = new(validation, new TestConfiguration(settings), certificateLoader);

        Assert.AreSame(clientInner, await client.OpenSecureStreamAsync(clientInner, TestContext.CancellationToken));
        Assert.AreSame(serverInner, await server.OpenSecureStreamAsync(serverInner, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task AutomaticProtocolSelection_NegotiatesTlsAsync()
    {
        await using CertificateFiles serverCertificate = await CertificateFiles.CreateAsync("daemon.test", serverAuthentication: true, TestContext.CancellationToken);
        await using TlsConnection connection = await OpenTlsPairAsync(
            serverCertificate,
            clientCertificate: null,
            protocols: SslProtocols.None,
            requireClientCertificate: false);
        Assert.AreNotEqual(SslProtocols.None, connection.Client.SslProtocol);
        Assert.AreEqual(connection.Client.SslProtocol, connection.Server.SslProtocol);
    }

    [TestMethod]
#pragma warning disable CA5398 // This test intentionally verifies that an explicit protocol restriction is honored.
    public async Task ExplicitProtocolRestriction_IsHonoredAsync()
    {
        await using CertificateFiles serverCertificate = await CertificateFiles.CreateAsync("daemon.test", serverAuthentication: true, TestContext.CancellationToken);
        await using TlsConnection connection = await OpenTlsPairAsync(
            serverCertificate,
            clientCertificate: null,
            protocols: SslProtocols.Tls12,
            requireClientCertificate: false);
        Assert.AreEqual(SslProtocols.Tls12, connection.Client.SslProtocol);
        Assert.AreEqual(SslProtocols.Tls12, connection.Server.SslProtocol);
    }
#pragma warning restore CA5398

    [TestMethod]
    public async Task MutualTls_WithClientCertificate_SucceedsAsync()
    {
        await using CertificateFiles serverCertificate = await CertificateFiles.CreateAsync("daemon.test", serverAuthentication: true, TestContext.CancellationToken);
        await using CertificateFiles clientCertificate = await CertificateFiles.CreateAsync("client.test", serverAuthentication: false, TestContext.CancellationToken);
        await using TlsConnection connection = await OpenTlsPairAsync(
            serverCertificate,
            clientCertificate,
            protocols: SslProtocols.None,
            requireClientCertificate: true);
        Assert.IsTrue(connection.Client.IsMutuallyAuthenticated);
        Assert.IsTrue(connection.Server.IsMutuallyAuthenticated);
    }

    [TestMethod]
    public async Task MutualTls_WithoutClientCertificate_FailsAsync()
    {
        await using CertificateFiles serverCertificate = await CertificateFiles.CreateAsync("daemon.test", serverAuthentication: true, TestContext.CancellationToken);
        await AssertHandshakeFailsAsync(async cancellationToken =>
        {
            _ = await OpenTlsPairAsync(
                serverCertificate,
                clientCertificate: null,
                protocols: SslProtocols.None,
                requireClientCertificate: true,
                cancellationToken);
        });
    }


    [TestMethod]
    public async Task Tls_UntrustedServerCertificate_IsRejectedByDefaultClientValidationAsync()
    {
        await using CertificateFiles serverCertificate = await CertificateFiles.CreateAsync("daemon.test", serverAuthentication: true, TestContext.CancellationToken);
        await AssertHandshakeFailsAsync(async cancellationToken =>
        {
            _ = await OpenTlsPairAsync(
                serverCertificate,
                clientCertificate: null,
                protocols: SslProtocols.None,
                requireClientCertificate: false,
                cancellationToken,
                clientValidationHandler: new ClientDefaultCertificateValidationHandler());
        });
    }

    [TestMethod]
    public async Task MutualTls_UntrustedClientCertificate_IsRejectedByProductionValidationAsync()
    {
        await using CertificateFiles serverCertificate = await CertificateFiles.CreateAsync("daemon.test", serverAuthentication: true, TestContext.CancellationToken);
        await using CertificateFiles clientCertificate = await CertificateFiles.CreateAsync("client.test", serverAuthentication: false, TestContext.CancellationToken);
        using X509Certificate2 certificate = X509Certificate2.CreateFromPem(await File.ReadAllTextAsync(clientCertificate.CertificatePath, TestContext.CancellationToken));
        AppSettings validationSettings = TestAppSettingsFactory.Create();
        validationSettings.Pipe.RemoteCertificateValidation = new RemoteCertificateValidationOptions
        {
            RequiredIssuer = certificate.Issuer,
            RequiredSubject = certificate.Subject,
        };
        ServerMutualTlsCertificateValidationHandler productionValidation = new(new TestConfiguration(validationSettings));

        await AssertHandshakeFailsAsync(async cancellationToken =>
        {
            _ = await OpenTlsPairAsync(
                serverCertificate,
                clientCertificate,
                protocols: SslProtocols.None,
                requireClientCertificate: true,
                cancellationToken,
                serverValidationHandler: productionValidation);
        });
    }

    [TestMethod]
    public void ClientDefaultValidation_RejectsUntrustedServerCertificate()
    {
        ClientDefaultCertificateValidationHandler handler = new();

        Assert.IsFalse(handler.ValidateCertificate(this, null, null, SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.IsTrue(handler.ValidateCertificate(this, null, null, SslPolicyErrors.None));
    }

    [TestMethod]
    public async Task MutualTlsValidation_RejectsUntrustedOrUnexpectedClientCertificateAsync()
    {
        await using CertificateFiles clientCertificate = await CertificateFiles.CreateAsync("client.test", serverAuthentication: false, TestContext.CancellationToken);
        using X509Certificate2 certificate = X509Certificate2.CreateFromPem(await File.ReadAllTextAsync(clientCertificate.CertificatePath, TestContext.CancellationToken));
        AppSettings settings = TestAppSettingsFactory.Create();
        settings.Pipe.TlsEnabled = true;
        settings.Pipe.ServerCertificatePath = clientCertificate.CertificatePath;
        settings.Pipe.ServerCertificateKeyPath = clientCertificate.KeyPath;
        settings.Pipe.RemoteCertificateValidation = new RemoteCertificateValidationOptions
        {
            RequiredIssuer = certificate.Issuer,
            RequiredSubject = certificate.Subject,
        };
        ServerMutualTlsCertificateValidationHandler handler = new(new TestConfiguration(settings));

        Assert.IsTrue(handler.ValidateCertificate(this, certificate, null, SslPolicyErrors.None));
        Assert.IsFalse(handler.ValidateCertificate(this, certificate, null, SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.IsFalse(handler.ValidateCertificate(this, null, null, SslPolicyErrors.RemoteCertificateNotAvailable));

        settings.Pipe.RemoteCertificateValidation.RequiredSubject = "CN=someone-else";
        Assert.IsFalse(handler.ValidateCertificate(this, certificate, null, SslPolicyErrors.None));
    }

    private async Task<TlsConnection> OpenTlsPairAsync(
        CertificateFiles serverCertificate,
        CertificateFiles? clientCertificate,
        SslProtocols protocols,
        bool requireClientCertificate,
        CancellationToken cancellationToken = default,
        ClientCertificateValidationHandler? clientValidationHandler = null,
        ServerCertificateValidationHandler? serverValidationHandler = null)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        (Stream clientInner, Stream serverInner) = DuplexStreamPair.Create();
        CertificateValidationHandler defaultValidation = new(accept: true, requireCertificate: requireClientCertificate);
        clientValidationHandler ??= defaultValidation;
        serverValidationHandler ??= defaultValidation;
        PemCertificateLoader certificateLoader = new();

        UfwClientOptions clientOptions = new(
            ServerName: ".",
            PipeName: "/tmp/ufw-tests.pipe",
            TlsEnabled: true,
            TlsServerName: "daemon.test",
            SslProtocols: protocols,
            IoTimeout: TimeSpan.FromSeconds(1),
            RequestTimeout: TimeSpan.FromSeconds(1),
            ClientCertificatePath: clientCertificate?.CertificatePath,
            ClientCertificateKeyPath: clientCertificate?.KeyPath);
        ClientTransportSecurityService? clientSecurity = null;
        ServerTransportSecurityService? serverSecurity = null;
        try
        {
            clientSecurity = new ClientTransportSecurityService(clientValidationHandler, certificateLoader, clientOptions);

            AppSettings settings = TestAppSettingsFactory.Create();
            settings.Pipe.TlsEnabled = true;
            settings.Pipe.SslProtocols = protocols;
            settings.Pipe.ServerCertificatePath = serverCertificate.CertificatePath;
            settings.Pipe.ServerCertificateKeyPath = serverCertificate.KeyPath;
            if (requireClientCertificate)
            {
                settings.Pipe.RemoteCertificateValidation = new RemoteCertificateValidationOptions
                {
                    RequiredIssuer = "test",
                    RequiredSubject = "test",
                };
            }
            serverSecurity = new ServerTransportSecurityService(serverValidationHandler, new TestConfiguration(settings), certificateLoader);

            Task<Stream> serverTask = serverSecurity.OpenSecureStreamAsync(serverInner, timeout.Token);
            Task<Stream> clientTask = clientSecurity.OpenSecureStreamAsync(clientInner, timeout.Token);
            await Task.WhenAll(serverTask, clientTask);
            TlsConnection connection = new(
                (SslStream)await clientTask,
                (SslStream)await serverTask,
                clientSecurity,
                serverSecurity,
                clientInner,
                serverInner);
            clientSecurity = null;
            serverSecurity = null;
            clientInner = Stream.Null;
            serverInner = Stream.Null;
            return connection;
        }
        finally
        {
            clientSecurity?.Dispose();
            serverSecurity?.Dispose();
            await clientInner.DisposeAsync();
            await serverInner.DisposeAsync();
        }
    }

    private async Task AssertHandshakeFailsAsync(Func<CancellationToken, Task> handshake)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await handshake(timeout.Token);
            Assert.Fail("TLS handshake unexpectedly succeeded.");
        }
        catch (OperationCanceledException) when (!TestContext.CancellationToken.IsCancellationRequested)
        {
            Assert.Fail("TLS handshake timed out rather than failing authentication.");
        }
        catch (AuthenticationException)
        {
        }
        catch (IOException)
        {
            // The peer can observe a transport close while the other side reports authentication failure.
        }
    }

    private sealed class CertificateValidationHandler(bool accept, bool requireCertificate = false) : ClientCertificateValidationHandler, ServerCertificateValidationHandler
    {
        public bool ValidateCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) =>
            accept && (!requireCertificate || certificate is not null);
    }

    private sealed class CertificateFiles : IAsyncDisposable
    {
        private CertificateFiles(string directory, string certificatePath, string keyPath)
        {
            Directory = directory;
            CertificatePath = certificatePath;
            KeyPath = keyPath;
        }

        private string Directory { get; }

        public string CertificatePath { get; }

        public string KeyPath { get; }

        public static async Task<CertificateFiles> CreateAsync(string commonName, bool serverAuthentication, CancellationToken cancellationToken)
        {
            string directory = Path.Combine(Path.GetTempPath(), $"ufw-tls-{Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(directory);
            string certificatePath = Path.Combine(directory, "cert.pem");
            string keyPath = Path.Combine(directory, "key.pem");

            using RSA key = RSA.Create(2048);
            CertificateRequest request = new(
                $"CN={commonName}",
                key,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            OidCollection usages = new()
            {
                new Oid(serverAuthentication ? "1.3.6.1.5.5.7.3.1" : "1.3.6.1.5.5.7.3.2"),
            };
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, true));
            if (serverAuthentication)
            {
                SubjectAlternativeNameBuilder san = new();
                san.AddDnsName(commonName);
                request.CertificateExtensions.Add(san.Build());
            }

            using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            await File.WriteAllTextAsync(certificatePath, certificate.ExportCertificatePem(), cancellationToken);
            await File.WriteAllTextAsync(keyPath, key.ExportPkcs8PrivateKeyPem(), cancellationToken);
            return new CertificateFiles(directory, certificatePath, keyPath);
        }

        public ValueTask DisposeAsync()
        {
            System.IO.Directory.Delete(Directory, recursive: true);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TlsConnection(
        SslStream client,
        SslStream server,
        ClientTransportSecurityService clientSecurity,
        ServerTransportSecurityService serverSecurity,
        Stream clientInner,
        Stream serverInner) : IAsyncDisposable
    {
        public SslStream Client { get; } = client;

        public SslStream Server { get; } = server;

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await Server.DisposeAsync();
            clientSecurity.Dispose();
            serverSecurity.Dispose();
            await clientInner.DisposeAsync();
            await serverInner.DisposeAsync();
        }
    }

    public TestContext TestContext { get; set; } = null!;
}
