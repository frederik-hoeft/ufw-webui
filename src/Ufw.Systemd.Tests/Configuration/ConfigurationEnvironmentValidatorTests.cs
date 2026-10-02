using Ufw.Systemd.Configuration;
using Ufw.Systemd.Configuration.Model;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Configuration;

[TestClass]
public sealed class ConfigurationEnvironmentValidatorTests
{
    [TestMethod]
    public void ThrowIfInvalid_RequiresConfiguredUfwExecutable()
    {
        AppSettings settings = CreateSettings(ufwPath: "/definitely/not/a/ufw-executable");
        ConfigurationEnvironmentValidator validator = new();

        Assert.ThrowsExactly<InvalidOperationException>(() => validator.ThrowIfInvalid(settings));
    }

    [TestMethod]
    public void ThrowIfInvalid_RejectsDirectoriesForEverySecurityFilePath()
    {
        string ufwPath = Path.GetTempFileName();
        string directory = Directory.CreateTempSubdirectory("ufw-security-options-").FullName;
        try
        {
            AppSettings[] settings =
            [
                CopyWithUfwPath(TestAppSettingsFactory.Create(authorizedKeysPath: directory), ufwPath),
                CopyWithUfwPath(TestAppSettingsFactory.Create(nonceStorePath: directory), ufwPath),
                CopyWithUfwPath(TestAppSettingsFactory.Create(deploymentIdPath: directory), ufwPath),
                CopyWithUfwPath(TestAppSettingsFactory.Create(reorderRecoveryJournalPath: directory), ufwPath),
            ];
            ConfigurationEnvironmentValidator validator = new();

            foreach (AppSettings setting in settings)
            {
                Assert.ThrowsExactly<InvalidOperationException>(() => validator.ThrowIfInvalid(setting));
            }
        }
        finally
        {
            File.Delete(ufwPath);
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ThrowIfInvalid_TlsRequiresExistingCertificateFilesForAnyTransport()
    {
        string ufwPath = Path.GetTempFileName();
        try
        {
            ConfigurationEnvironmentValidator validator = new();
            foreach (TransportType transportType in Enum.GetValues<TransportType>())
            {
                AppSettings settings = CopyWithUfwPath(TestAppSettingsFactory.Create(
                    transportType: transportType,
                    tlsEnabled: true,
                    serverCertificatePath: "/definitely/not/a/certificate.pem",
                    serverCertificateKeyPath: "/definitely/not/a/key.pem"), ufwPath);

                Assert.ThrowsExactly<InvalidOperationException>(() => validator.ThrowIfInvalid(settings));
            }
        }
        finally
        {
            File.Delete(ufwPath);
        }
    }

    [TestMethod]
    public void ThrowIfInvalid_RejectsRelativeSelectedPipePathOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows named pipes use logical pipe names rather than Unix socket paths.");
        }

        string ufwPath = Path.GetTempFileName();
        try
        {
            AppSettings settings = CopyWithUfwPath(TestAppSettingsFactory.Create(pipeName: "relative-pipe-name"), ufwPath);
            ConfigurationEnvironmentValidator validator = new();

            Assert.ThrowsExactly<InvalidOperationException>(() => validator.ThrowIfInvalid(settings));
        }
        finally
        {
            File.Delete(ufwPath);
        }
    }

    [TestMethod]
    public void ThrowIfInvalid_DoesNotApplyPipePathRulesWhenTcpIsSelectedOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string ufwPath = Path.GetTempFileName();
        try
        {
            AppSettings baseline = TestAppSettingsFactory.Create(transportType: TransportType.Tcp);
            TransportOptions transport = new()
            {
                Type = TransportType.Tcp,
                Pipe = new PipeOptions { PipeName = "relative-unused-pipe" },
                Tcp = baseline.Transport.Tcp,
                Security = baseline.Transport.Security,
            };
            AppSettings settings = CopyWithUfwPath(baseline, ufwPath, transport);
            ConfigurationEnvironmentValidator validator = new();

            validator.ThrowIfInvalid(settings);
        }
        finally
        {
            File.Delete(ufwPath);
        }
    }

    private static AppSettings CreateSettings(string ufwPath) =>
        CopyWithUfwPath(TestAppSettingsFactory.Create(), ufwPath);

    private static AppSettings CopyWithUfwPath(AppSettings baseline, string ufwPath, TransportOptions? transport = null) =>
        new()
        {
            DebugMode = baseline.DebugMode,
            ExposeRemoteExceptionDetails = baseline.ExposeRemoteExceptionDetails,
            UfwPath = ufwPath,
            UfwDefaultsPath = baseline.UfwDefaultsPath,
            Transport = transport ?? baseline.Transport,
            Network = baseline.Network,
            Security = baseline.Security,
        };
}
