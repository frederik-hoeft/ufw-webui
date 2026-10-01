using System.Security.Authentication;
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
    public void ThrowIfInvalid_TlsRequiresExistingCertificateFiles()
    {
        string ufwPath = Path.GetTempFileName();
        try
        {
            AppSettings baseline = TestAppSettingsFactory.Create();
            PipeOptions pipe = new()
            {
                PipeName = baseline.Pipe.PipeName,
                TlsEnabled = true,
                SslProtocols = SslProtocols.None,
                RemoteCertificateValidation = null,
                ServerCertificatePath = "/definitely/not/a/certificate.pem",
                ServerCertificateKeyPath = "/definitely/not/a/key.pem",
            };
            AppSettings settings = CopyWithUfwPath(baseline, ufwPath, pipe);
            ConfigurationEnvironmentValidator validator = new();

            Assert.ThrowsExactly<InvalidOperationException>(() => validator.ThrowIfInvalid(settings));
        }
        finally
        {
            File.Delete(ufwPath);
        }
    }

    [TestMethod]
    public void ThrowIfInvalid_RejectsRelativePipePathOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows named pipes use logical pipe names rather than Unix socket paths.");
        }

        string ufwPath = Path.GetTempFileName();
        try
        {
            AppSettings baseline = TestAppSettingsFactory.Create();
            PipeOptions pipe = new()
            {
                PipeName = "relative-pipe-name",
                TlsEnabled = false,
                SslProtocols = SslProtocols.None,
                RemoteCertificateValidation = null,
                ServerCertificatePath = null,
                ServerCertificateKeyPath = null,
            };
            AppSettings settings = CopyWithUfwPath(baseline, ufwPath, pipe);
            ConfigurationEnvironmentValidator validator = new();

            Assert.ThrowsExactly<InvalidOperationException>(() => validator.ThrowIfInvalid(settings));
        }
        finally
        {
            File.Delete(ufwPath);
        }
    }

    private static AppSettings CreateSettings(string ufwPath)
    {
        AppSettings baseline = TestAppSettingsFactory.Create();
        return CopyWithUfwPath(baseline, ufwPath);
    }

    private static AppSettings CopyWithUfwPath(AppSettings baseline, string ufwPath, PipeOptions? pipe = null) =>
        new()
        {
            DebugMode = baseline.DebugMode,
            ExposeRemoteExceptionDetails = baseline.ExposeRemoteExceptionDetails,
            UfwPath = ufwPath,
            UfwDefaultsPath = baseline.UfwDefaultsPath,
            Pipe = pipe ?? baseline.Pipe,
            Network = baseline.Network,
            Security = baseline.Security,
        };
}
