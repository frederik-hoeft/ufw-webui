using Ufw.Systemd.Persistence;
using Ufw.Systemd.Security.Intent;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Security.Intent;

[TestClass]
public sealed class FileDeploymentIdentityProviderTests
{
    [TestMethod]
    public void GetDeploymentId_CreatesStableIdentityAndSurvivesReload()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ufw-deployment-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "deployment-id");
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(deploymentIdPath: path));

        try
        {
            FileDeploymentIdentityProvider first = new(configuration, new DurableFileStore());
            string created = first.GetDeploymentId();
            Assert.IsFalse(string.IsNullOrWhiteSpace(created));
            Assert.AreEqual(created, first.GetDeploymentId());

            FileDeploymentIdentityProvider reloaded = new(configuration, new DurableFileStore());
            Assert.AreEqual(created, reloaded.GetDeploymentId());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void GetDeploymentId_RejectsCorruptPersistedIdentity()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ufw-deployment-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "deployment-id");
        File.WriteAllText(path, "not-base64url!!!!");
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(deploymentIdPath: path));

        try
        {
            FileDeploymentIdentityProvider provider = new(configuration, new DurableFileStore());
            Assert.ThrowsExactly<InvalidDataException>(provider.GetDeploymentId);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
