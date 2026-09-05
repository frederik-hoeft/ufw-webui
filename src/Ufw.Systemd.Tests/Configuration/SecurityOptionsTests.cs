using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Tests.Configuration;

[TestClass]
public sealed class SecurityOptionsTests
{
    [TestMethod]
    public void TestAssertIsValid_RejectsDirectoryFilePaths()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ufw-security-options-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            SecurityOptions authorizedKeys = new() { AuthorizedKeysPath = directory };
            Assert.ThrowsExactly<InvalidOperationException>(() => authorizedKeys.AssertIsValid());

            SecurityOptions nonceStore = new() { NonceStorePath = directory };
            Assert.ThrowsExactly<InvalidOperationException>(() => nonceStore.AssertIsValid());

            SecurityOptions deploymentId = new() { DeploymentIdPath = directory };
            Assert.ThrowsExactly<InvalidOperationException>(() => deploymentId.AssertIsValid());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
