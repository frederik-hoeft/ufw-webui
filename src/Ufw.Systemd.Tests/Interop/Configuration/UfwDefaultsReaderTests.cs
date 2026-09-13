using Ufw.Shared.Firewall;
using Ufw.Systemd.Interop.Configuration;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Interop.Configuration;

[TestClass]
public sealed class UfwDefaultsReaderTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ReadAsync_ReadsConfiguredDefaultsFileAsync()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ufw-defaults-{Guid.NewGuid():N}");
        try
        {
            await File.WriteAllTextAsync(path, """
                IPV6=no
                DEFAULT_INPUT_POLICY=DROP
                DEFAULT_OUTPUT_POLICY=ACCEPT
                DEFAULT_FORWARD_POLICY=DROP
                """, TestContext.CancellationToken);
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(ufwDefaultsPath: path));
            UfwDefaultsReader reader = new(configuration, new ConsoleLogger());

            FirewallConfigurationSnapshot? snapshot = await reader.ReadAsync(TestContext.CancellationToken);

            Assert.IsNotNull(snapshot);
            Assert.IsFalse(snapshot.IPv6Enabled);
            Assert.AreEqual(FirewallDefaultPolicy.Deny, snapshot.IncomingPolicy);
            Assert.AreEqual(FirewallDefaultPolicy.Allow, snapshot.OutgoingPolicy);
            Assert.AreEqual(FirewallDefaultPolicy.Deny, snapshot.RoutedPolicy);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ReadAsync_MissingOrMalformedFile_FailsClosedAsync()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ufw-defaults-{Guid.NewGuid():N}");
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(ufwDefaultsPath: path));
        UfwDefaultsReader reader = new(configuration, new ConsoleLogger());

        Assert.IsNull(await reader.ReadAsync(TestContext.CancellationToken));

        await File.WriteAllTextAsync(path, "IPV6=yes\n", TestContext.CancellationToken);
        try
        {
            Assert.IsNull(await reader.ReadAsync(TestContext.CancellationToken));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
