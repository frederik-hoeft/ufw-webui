using System.Text.Json;
using Moq;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Tests.Configuration;

[TestClass]
public sealed class ConfigurationImplTests
{
    [TestMethod]
    public void Settings_BeforeLoad_Throws()
    {
        ConfigurationImpl configuration = CreateConfiguration();

        Assert.ThrowsExactly<InvalidOperationException>(() => _ = configuration.Settings);
    }

    [TestMethod]
    public async Task LoadAsync_LoadsValidatedSettingsExactlyOnceAsync()
    {
        string path = await WriteSettingsAsync();
        try
        {
            Mock<IConfigurationEnvironmentValidator> environmentValidator = new(MockBehavior.Strict);
            environmentValidator
                .Setup(validator => validator.ThrowIfInvalid(It.IsAny<AppSettings>()));
            ConfigurationImpl configuration = CreateConfiguration(environmentValidator.Object);

            await configuration.LoadAsync(path, CancellationToken.None);

            Assert.AreEqual("/usr/sbin/ufw", configuration.Settings.UfwPath);
            Assert.AreEqual(TimeSpan.FromMinutes(30), configuration.Settings.Network.RequestTimeout);
            environmentValidator.Verify(
                validator => validator.ThrowIfInvalid(It.Is<AppSettings>(settings => settings.Network.MaxConnections == 8)),
                Times.Once);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
                await configuration.LoadAsync(path, CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task LoadAsync_MissingRequiredSetting_DoesNotFallBackToModelDefaultAsync()
    {
        string path = await WriteSettingsAsync(includeRequestTimeout: false);
        try
        {
            Mock<IConfigurationEnvironmentValidator> environmentValidator = new(MockBehavior.Strict);
            ConfigurationImpl configuration = CreateConfiguration(environmentValidator.Object);

            await Assert.ThrowsExactlyAsync<JsonException>(async () =>
                await configuration.LoadAsync(path, CancellationToken.None));
            environmentValidator.VerifyNoOtherCalls();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ConfigurationImpl CreateConfiguration(IConfigurationEnvironmentValidator? environmentValidator = null) =>
        new(
            AppSettingsJsonSerializerContext.Default,
            environmentValidator ?? Mock.Of<IConfigurationEnvironmentValidator>());

    private static async Task<string> WriteSettingsAsync(bool includeRequestTimeout = true)
    {
        string requestTimeout = includeRequestTimeout ? ",\n    \"request_timeout\": \"00:30:00\"" : string.Empty;
        string json = $$"""
        {
          "debug_mode": true,
          "expose_remote_exception_details": false,
          "ufw_path": "/usr/sbin/ufw",
          "ufw_defaults_path": "/etc/default/ufw",
          "pipe": {
            "pipe_name": "/tmp/ufw-systemd-tests.pipe",
            "tls_enabled": false,
            "ssl_protocols": "none",
            "remote_certificate_validation": null,
            "server_certificate_path": null,
            "server_certificate_key_path": null
          },
          "network": {
            "max_connections": 8,
            "io_timeout": "00:00:30"{{requestTimeout}}
          },
          "security": null
        }
        """;
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, json);
        return path;
    }
}
